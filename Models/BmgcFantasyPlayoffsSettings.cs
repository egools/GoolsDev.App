namespace GoolsDev.App.Models
{
    public class BmgcFantasyPlayoffsSettings
    {
        public int CurrentNflSeason { get; set; }
        public int CurrentSeasonType { get; set; }
        public ScoringType ScoringType { get; set; }
    }

    public enum ScoringType
    {
        Standard = 1,
        BestBall = 2
    }
}
