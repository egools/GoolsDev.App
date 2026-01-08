namespace GoolsDev.App.Models
{
    public class BmgcFantasyPlayoffsSettings
    {
        public int CurrentNflSeason { get; set; }
        public NflSeasonType CurrentSeasonType { get; set; }
    }

    public enum NflSeasonType
    {
        Preseason = 1,
        RegularSeason = 2,
        Playoffs = 3
    }
}
