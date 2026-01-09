using System.Collections.Generic;

namespace GoolsDev.App.Models
{
    public class FantasySeason
    {
        public ScoringType ScoringType { get; set; }
        public bool IsBestBall { get; set; }
        public List<string> RosterPositions { get; set; }
        public List<PlayoffFantasyTeam> Teams { get; set; }
    }

    public enum ScoringType
    {
        Standard = 1,
        Weekly = 2
    }

    public class PlayoffFantasyTeam
    {
        public string Name { get; set; }
        public Dictionary<string, string> StandardRoster { get; set; }
        public Dictionary<int, List<string>> WeeklyRosters { get; set; }
    }
}
