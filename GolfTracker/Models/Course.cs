using SQLite;

namespace GolfTracker.Models;

public class Course
{
    [PrimaryKey, AutoIncrement]
    public int ID { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    // e.g., "Standard", "Short", "Putting"
    public string CourseType { get; set; } = "Standard";

    // Typically 9 or 18 (or custom hole counts for putting/short courses)
    public int HoleCount { get; set; } = 18;

    public int TotalPar { get; set; }

    public bool NeedsSync { get; set; } = true;
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
}
