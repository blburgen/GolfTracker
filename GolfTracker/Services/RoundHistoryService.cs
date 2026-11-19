
using GolfTracker.Pages;

namespace GolfTracker.Services;

public class RoundHistoryService
{
    public static RoundHistoryService Instance { get; } = new();

    public List<GolfRound> Rounds { get; } = new();

    public void AddRound(GolfRound round)
    {
        Rounds.Add(round);
    }
}
