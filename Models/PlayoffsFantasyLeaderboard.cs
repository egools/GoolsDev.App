using GoolsDev.App.Cosmos;
using System.Collections.Generic;
using System.Linq;

namespace GoolsDev.App.Models
{
    public class PlayoffsFantasyLeaderboard
    {
        public IOrderedEnumerable<int> Weeks { get; set; }
        public List<LeaderboardTeam> FantasyTeams { get; set; } = new List<LeaderboardTeam>();
    }

    public class LeaderboardTeam
    {
        public string Name { get; set; }
        public double TotalPoints { get; set; }
        public List<LeaderboardPlayer> Players { get; set; } = new List<LeaderboardPlayer>();
    }

    public class LeaderboardPlayer
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string JerseyNumber { get; set; }
        public string TeamShortName { get; set; }
        public string Postion { get; set; }
        public string RosterPosition { get; set; }
        public string CurrentGameStatus { get; set; }
        public List<LeaderboardStatline> Statlines { get; set; } = new List<LeaderboardStatline>();
    }

    public record LeaderboardStatline : NflPlayerStatline
    {
        public LeaderboardStatline() : base(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0) { }
        public LeaderboardStatline(NflPlayerStatline original) : base(original) { }

        public string GameStatus { get; set; }
    }
}
