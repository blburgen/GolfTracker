
namespace GolfTracker.Pages;

public class GolfRound
{
    public string CourseName { get; set; } = "";
    public DateTime Date { get; set; }
    public string CourseType { get; set; } = "";

    public int NumberOfHoles { get; set; }
    public int HolesCompleted { get; set; }
    public int TotalStrokes { get; set; }

    public bool EndedEarly { get; set; }

    public int?[] HoleStrokes { get; set; } = Array.Empty<int?>();
    public int[] HolePars { get; set; } = Array.Empty<int>();
}