using Ecclesia.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ImportStatus>();
builder.Services.AddDbContext<AppDb>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Ecclesia"))
);
builder.Services.AddHttpClient<OsmImporter>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(90);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Ecclesia/1.0");
});
builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p =>
        p.WithOrigins(
                builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                    ?? ["http://localhost:4200"]
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
    )
);
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    try
    {
        await db.Database.EnsureCreatedAsync();
        // EnsureCreated does not evolve existing databases; add the two optional OSM fields.
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Churches" ADD COLUMN IF NOT EXISTS "OpeningHours" text;
            ALTER TABLE "Churches" ADD COLUMN IF NOT EXISTS "ServiceTimes" text;
            """);
        await db.Churches.AsNoTracking().Take(1).ToListAsync();
        await db.Set<Celebration>().AsNoTracking().Take(1).ToListAsync();
        await db.Set<CelebrationException>().AsNoTracking().Take(1).ToListAsync();
        await db.Imports.AsNoTracking().Take(1).ToListAsync();
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            "Initialisation ou verification de la base impossible. Verifiez la connexion et le schema ; consultez le README.", ex
        );
    }
}
if (args.Contains("--seed-demo"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Churches.AnyAsync(c => c.IsDemo))
    {
        db.Churches.Add(
            new Church
            {
                Name = "Église de démonstration (fictive)",
                Latitude = 50.6326,
                Longitude = 5.5797,
                Confession = "Catholique",
                IsDemo = true,
                UpdatedAt = DateTimeOffset.UtcNow,
                Celebrations =
                [
                    new Celebration
                    {
                        Rite = "Romain",
                        Type = "Messe",
                        Language = "Français",
                        DayOfWeek = 0,
                        Time = new TimeOnly(10, 0),
                        SourceUrl = "",
                        VerifiedAt = DateTimeOffset.UtcNow,
                    },
                ],
            }
        );
        await db.SaveChangesAsync();
    }
    return;
}
if (args.Contains("--import-osm"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<OsmImporter>().ImportAsync(args.Contains("--force"));
    return;
}
app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
{
    var status = app.Services.GetRequiredService<ImportStatus>();
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OsmImporter>().ImportAsync();
        status.State = "complete";
    }
    catch (Exception ex)
    {
        status.State = "failed";
        app.Logger.LogError(ex, "Import OSM impossible ; les données existantes restent disponibles. Relancez l'API pour réessayer.");
    }
}));
app.UseCors();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/import-status", (ImportStatus status) => Results.Ok(new { status = status.State }));
app.MapGet(
    "/api/churches",
    async (
        double lat,
        double lon,
        double radius,
        string? confession,
        string? jurisdiction,
        string? rite,
        string? type,
        string? language,
        int? day,
        string? from,
        string? to,
        bool? accessible,
        bool? parking,
        string? dataset,
        AppDb db
    ) =>
    {
        if (
            lat is < -90 or > 90
            || lon is < -180 or > 180
            || radius is <= 0 or > 100
            || day is < 0 or > 6
            || (from != null && !TimeOnly.TryParse(from, out _))
            || (to != null && !TimeOnly.TryParse(to, out _))
        )
            return Results.BadRequest(new { error = "Paramètres invalides." });
        var dLat = radius / 111.0;
        var dLon = Math.Min(180, radius / (111 * Math.Max(.01, Math.Cos(lat * Math.PI / 180))));
        var query = db
            .Churches.AsNoTracking()
            .Include(c => c.Celebrations)
            .Where(c =>
                c.IsDemo == (dataset == "demo")
                && c.Latitude >= lat - dLat
                && c.Latitude <= lat + dLat
                && c.Longitude >= lon - dLon
                && c.Longitude <= lon + dLon
            );
        if (!string.IsNullOrEmpty(confession))
            query = query.Where(c => c.Confession == confession);
        if (!string.IsNullOrEmpty(jurisdiction))
            query = query.Where(c => c.Jurisdiction == jurisdiction);
        if (accessible == true)
            query = query.Where(c => c.Accessible == true);
        if (parking == true)
            query = query.Where(c => c.Parking == true);
        if (
            rite != null
            || type != null
            || language != null
            || day != null
            || from != null
            || to != null
        )
        {
            var start = from == null ? (TimeOnly?)null : TimeOnly.Parse(from);
            var end = to == null ? (TimeOnly?)null : TimeOnly.Parse(to);
            query = query.Where(c =>
                c.Celebrations.Any(s =>
                    (rite == null || s.Rite == rite)
                    && (type == null || s.Type == type)
                    && (language == null || s.Language == language)
                    && (day == null || s.DayOfWeek == day)
                    && (start == null || s.Time >= start)
                    && (end == null || s.Time <= end)
                )
            );
        }
        var churches = await query.ToListAsync();
        return Results.Ok(
            churches
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Latitude,
                    c.Longitude,
                    c.Address,
                    c.Website,
                    c.OpeningHours,
                    c.ServiceTimes,
                    c.Confession,
                    c.Jurisdiction,
                    c.Accessible,
                    c.Parking,
                    c.IsDemo,
                    c.OsmKey,
                    c.UpdatedAt,
                    DistanceKm = Geo.Distance(lat, lon, c.Latitude, c.Longitude),
                    Celebrations = c.Celebrations.Select(s => new
                    {
                        s.Id,
                        s.Rite,
                        s.Type,
                        s.Language,
                        s.DayOfWeek,
                        Time = s.Time.ToString("HH:mm"),
                        s.SourceUrl,
                        s.VerifiedAt,
                    }),
                })
                .Where(c => c.DistanceKm <= radius)
                .OrderBy(c => c.DistanceKm)
        );
    }
);
app.Run();

public partial class Program { }

public class ImportStatus
{
    public volatile string State = "running";
}
