using GoolsDev.App.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace GoolsDev.App.Models
{
    public abstract class PlayoffsFantasyLeaderboard
    {
        public bool IsTeamPage { get; set; } = false;
        public string PlayerPageName { get; set; }
        public int CurrentSeasonType { get; set; }
        public IEnumerable<string> RosterPositions { get; set; }
        public IOrderedEnumerable<int> Weeks { get; set; }
        public abstract string ViewName { get; }

        public abstract void Build(
            FantasySeason fantasySeason,
            List<NflPlayerStatsDocument> playerDocs,
            List<NflGameDocument> gameDocs);
    }

    public abstract class LeaderboardTeam
    {
        public string Name { get; set; }
        public double TotalPoints { get; set; }
        public int Rank { get; set; }
    }

    public abstract class LeaderboardPlayer
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string JerseyNumber { get; set; }
        public string TeamShortName { get; set; }
        public string Position { get; set; }
        public string RosterPosition { get; set; }
        public bool IsEliminated { get; set; } = false;

        public int PositionIndex =>
            RosterPosition switch
            {
                "QB" => 1,
                "RB" => 2,
                "RB1" => 3,
                "RB2" => 4,
                "WR" => 5,
                "WR1" => 6,
                "WR2" => 7,
                "TE" => 8,
                "TE1" => 9,
                "TE2" => 10,
                "FL" => 11,
                "FL1" => 12,
                "FL2" => 13,
                _ => 99
            };
    }

    public record LeaderboardStatline : NflPlayerStatline
    {
        public LeaderboardStatline() : base(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0) { }
        public LeaderboardStatline(NflPlayerStatline original) : base(original) { }
        public string GameStatus { get; set; }
        public bool HasGame { get; set; }
    }
}
