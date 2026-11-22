using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Ecclesia.Api;

public class OsmImporter(AppDb db, HttpClient http, ILogger<OsmImporter> logger)
{
    public async Task ImportAsync(bool force = false)
    {
        await db.Database.EnsureCreatedAsync();
        var state = await db.Imports.FirstOrDefaultAsync();
        if (!force && state != null && state.LastSuccessfulImport > DateTimeOffset.UtcNow.AddHours(-24))
        {
            logger.LogInformation("Cache valide 24 heures.");
            return;
        }
        const string query =
            "[out:json][timeout:60];(nwr[\"amenity\"=\"place_of_worship\"][\"religion\"=\"christian\"](50.55,5.45,50.70,5.70););out center tags;";
        using var response = await http.PostAsync(
            "https://overpass-api.de/api/interpreter",
            new FormUrlEncodedContent(new Dictionary<string, string> { { "data", query } })
        );
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        if (doc.RootElement.TryGetProperty("remark", out var remark))
            throw new InvalidOperationException($"Réponse Overpass incomplète : {remark.GetString()}");
        var existing = await db
            .Churches.Where(c => c.OsmKey != null)
            .ToDictionaryAsync(c => c.OsmKey!);
        var count = 0;
        foreach (var item in doc.RootElement.GetProperty("elements").EnumerateArray())
        {
            var key =
                item.GetProperty("type").GetString() + "/" + item.GetProperty("id").GetInt64();
            var loc = item.TryGetProperty("center", out var center) ? center : item;
            if (!loc.TryGetProperty("lat", out var lat) || !loc.TryGetProperty("lon", out var lon))
                continue;
            var tags = item.GetProperty("tags");
            string? Tag(string name) => tags.TryGetProperty(name, out var v) ? v.GetString() : null;
            if (!existing.TryGetValue(key, out var c))
            {
                c = new Church { OsmKey = key };
                db.Churches.Add(c);
                existing[key] = c;
            }
            c.Name = Tag("name") ?? "Église sans nom renseigné";
            c.Latitude = lat.GetDouble();
            c.Longitude = lon.GetDouble();
            c.Address = Tag("addr:full") ?? string.Join(
                " ",
                new[]
                {
                    Tag("addr:street"),
                    Tag("addr:housenumber"),
                    Tag("addr:postcode"),
                    Tag("addr:city"),
                }.Where(x => !string.IsNullOrWhiteSpace(x))
            );
            c.Website = Tag("website") ?? Tag("contact:website");
            c.OpeningHours = Tag("opening_hours");
            c.ServiceTimes = Tag("service_times");
            c.Confession = Tag("denomination") switch
            {
                "catholic" or "roman_catholic" => "Catholique",
                "orthodox" => "Orthodoxe",
                "protestant" => "Protestant",
                var denomination => denomination,
            };
            c.Accessible = Tag("wheelchair") switch
            {
                "yes" => true,
                "no" => false,
                _ => null,
            };
            c.UpdatedAt = DateTimeOffset.UtcNow;
            count++;
        }
        if (state == null)
        {
            state = new ImportState();
            db.Imports.Add(state);
        }
        state.LastSuccessfulImport = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        logger.LogInformation("{Count} lieux importés.", count);
    }
}
