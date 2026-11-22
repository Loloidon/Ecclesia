using Microsoft.EntityFrameworkCore;

namespace Ecclesia.Api;

public class Church
{
    public int Id { get; set; }
    public string? OsmKey { get; set; }
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Address { get; set; }
    public string? Website { get; set; }
    public string? OpeningHours { get; set; }
    public string? ServiceTimes { get; set; }
    public string? Confession { get; set; }
    public string? Jurisdiction { get; set; }
    public bool? Accessible { get; set; }
    public bool? Parking { get; set; }
    public bool IsDemo { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<Celebration> Celebrations { get; set; } = [];
}

public class Celebration
{
    public int Id { get; set; }
    public int ChurchId { get; set; }
    public string? Rite { get; set; }
    public string? Type { get; set; }
    public string? Language { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly Time { get; set; }
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public string SourceUrl { get; set; } = "";
    public DateTimeOffset VerifiedAt { get; set; }
    public List<CelebrationException> Exceptions { get; set; } = [];
}

public class CelebrationException
{
    public int Id { get; set; }
    public int CelebrationId { get; set; }
    public DateOnly Date { get; set; }
    public bool Cancelled { get; set; }
    public TimeOnly? ReplacementTime { get; set; }
}

public class ImportState
{
    public int Id { get; set; }
    public DateTimeOffset LastSuccessfulImport { get; set; }
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Church> Churches => Set<Church>();
    public DbSet<ImportState> Imports => Set<ImportState>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Church>().HasIndex(x => x.OsmKey).IsUnique();
        b.Entity<Church>().HasMany(x => x.Celebrations).WithOne().HasForeignKey(x => x.ChurchId);
        b.Entity<Celebration>()
            .HasMany(x => x.Exceptions)
            .WithOne()
            .HasForeignKey(x => x.CelebrationId);
    }
}

public static class Geo
{
    public static double Distance(double a, double b, double c, double d)
    {
        const double r = Math.PI / 180;
        var x =
            Math.Pow(Math.Sin((c - a) * r / 2), 2)
            + Math.Cos(a * r) * Math.Cos(c * r) * Math.Pow(Math.Sin((d - b) * r / 2), 2);
        return 12742 * Math.Atan2(Math.Sqrt(x), Math.Sqrt(Math.Max(0, 1 - x)));
    }
}
