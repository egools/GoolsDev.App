using System.Collections.Generic;
namespace GoolsDev.App.Models
{
    public record PickemKeyResults(
        Dictionary<int, Dictionary<string, PickemGameResult>> NflGameResults,
        Dictionary<int, YardagePropResult> YardagePropResults
    );

    public record PickemGameResult(
    string AwayTeam,
    string HomeTeam,
    int AwayScore,
    int HomeScore,
    List<string> WinningPicks,
    List<string> PushPicks
);

    public record YardagePropResult(
        string Header,
        string StatType,
        List<PlayerYards> Players,
        int ActualYards);

    public record PlayerYards(
        string Name,
        string Team,
        int RushingYards,
        int ReceivingYards);
}