using System.Collections.Generic;
namespace GoolsDev.App.Models
{
    public class PickemData
    {
        public List<int> Weeks { get; set; }
        public List<string> PickerNames { get; set; }
        public string ChartDataJson { get; set; }
        public Dictionary<string, List<Pick>> Picks { get; set; }
        public Dictionary<string, PropResult> PropResults { get; set;  }
        public Dictionary<string, PickemScore> PickemScores { get; set; }
        public Dictionary<string, List<ParlayPick>> ParlayResults { get; set; }
    }

    public record Pick(
        int Week,
        string KeyPick,
        int? YardagePick,
        string ParlayPick,
        string BigTenSurvivor,
        string NflSurvivor,
        Dictionary<string, string> NflPicks
    );

    public record ParlayPick(
       int Week,
       string Player,
       bool Scored
    );

    public record PickemScoreboard(
        List<int> Weeks,
        List<string> Pickers,
        Dictionary<string, PropResult> PropResults,
        Dictionary<string, PickemScore> PickemScores);

    public record PropResult(
        string Prop,
        int ActualYardage,
        int ClosestMiss,
        List<string> Winners,
        Dictionary<string, int> Guesses);

    public record PickemScore(
        Dictionary<string, PickemWeekResult> WeekResults,
        int GamePoints,
        int PropPoints,
        int TotalPoints);

    public record PickemWeekResult(
        int GamePoints,
        int? YardageGuess,
        int PropPoints,
        int WeekTotal,
        bool KeyPickCorrect);
}