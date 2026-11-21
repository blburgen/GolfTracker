using SQLite;

namespace GolfTracker.Models;

public class Round
{
    [PrimaryKey, AutoIncrement]
    public int ID { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string CourseType { get; set; } = string.Empty;

    public DateTime DatePlayed { get; set; }

    public int NumberOfHoles { get; set; }

    public int TotalStrokes { get; set; }

    // Nullable because holePars can be null on a scorecard
    public int? TotalPar { get; set; }

    public bool IsCompleted { get; set; }
}
