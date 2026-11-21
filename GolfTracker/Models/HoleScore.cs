using SQLite;

namespace GolfTracker.Models;

public class HoleScore
{
    [PrimaryKey, AutoIncrement]
    public int ID { get; set; }

    [Indexed]
    public int RoundID { get; set; }

    public int HoleNumber { get; set; }

    // Nullable to match holePars?[i]
    public int? Par { get; set; }

    // Nullable to match holeStrokes[i] (for unfinished rounds)
    public int? Strokes { get; set; }
}
