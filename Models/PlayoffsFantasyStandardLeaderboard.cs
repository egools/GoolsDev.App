using GoolsDev.App.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GoolsDev.App.Models
{
    public class PlayoffsFantasyStandardLeaderboard : PlayoffsFantasyLeaderboard
    {
        public override string ViewName => "StandardLeaderboard";
        public List<StandardLeaderboardTeam> FantasyTeams { get; set; } = [];

        public PlayoffsFantasyStandardLeaderboard(
            IEnumerable<string> rosterPositions,
            int currentSeasonType)
        {
            RosterPositions = rosterPositions;
            CurrentSeasonType = currentSeasonType;
        }

        public override void Build(
            FantasySeason fantasySeason,
            List<NflPlayerStatsDocument> playerDocs,
            List<NflGameDocument> gameDocs)
        {
            var teamGames = new Dictionary<string, (NflGameDocument, NflGameTeam, NflGameTeam, bool)>();
            gameDocs.ForEach(g =>
            {
                if (g.HomeTeam.IsActive) teamGames.Add($"{g.HomeTeam.TeamId}.{g.Week}", (g, g.HomeTeam, g.AwayTeam, true));
                if (g.AwayTeam.IsActive) teamGames.Add($"{g.AwayTeam.TeamId}.{g.Week}", (g, g.AwayTeam, g.HomeTeam, false));
            });
            Weeks = gameDocs.Select(g => g.Week).Distinct().OrderBy(w => w);

            foreach (var fantasyTeam in fantasySeason.Teams)
            {
                var leaderboardTeam = new StandardLeaderboardTeam
                {
                    Name = fantasyTeam.Name,
                    TotalPoints = 0
                };
                foreach (var (rosterPosition, id) in fantasyTeam.StandardRoster)
                {
                    var playerDoc = playerDocs.FirstOrDefault(p => p.PlayerId == id);
                    if (playerDoc is null) continue;

                    var player = new StandardLeaderboardPlayer
                    {
                        FirstName = playerDoc.FirstName,
                        LastName = playerDoc.LastName,
                        TeamShortName = playerDoc.TeamShortName,
                        JerseyNumber = playerDoc.JerseyNumber,
                        Position = playerDoc.Position,
                        RosterPosition = rosterPosition,
                    };

                    foreach (var week in Weeks)
                    {
                        var statline = playerDoc.Statlines.FirstOrDefault(s => s.Week == week && s.SeasonType == CurrentSeasonType);
                        var leaderboardStatline = statline is not null
                            ? new LeaderboardStatline(statline)
                            : new LeaderboardStatline()
                            {
                                Week = week,
                                SeasonType = CurrentSeasonType,
                                GameStatus = "No Game"
                            };

                        var playerGameDoc = gameDocs.FirstOrDefault(g => (g.HomeTeam.TeamId == playerDoc.TeamId || g.AwayTeam.TeamId == playerDoc.TeamId) && g.Week == week);
                        if (playerGameDoc is not null)
                        {
                            leaderboardStatline.GenerateGameStatus(playerGameDoc, playerDoc.TeamId);
                            player.CurrentGameStatus = leaderboardStatline.GameStatus;
                        }

                        player.CurrentGameStatus = leaderboardStatline.GameStatus;
                        player.Statlines.Add(leaderboardStatline);
                    }

                    leaderboardTeam.TotalPoints += player.Statlines.Sum(p => p.FantasyPoints);
                    leaderboardTeam.Players.Add(player);
                }

                FantasyTeams.Add(leaderboardTeam);
            }

            var rank = 1;
            foreach (var team in FantasyTeams.OrderByDescending(t => t.TotalPoints))
            {
                team.Rank = rank++;
            }
        }
    }

    public class StandardLeaderboardTeam : LeaderboardTeam
    {
        public List<StandardLeaderboardPlayer> Players { get; set; } = [];
        public StandardLeaderboardPlayer this[string position] => Players.FirstOrDefault(p => p.RosterPosition == position) ?? new();
    }

    public class StandardLeaderboardPlayer : LeaderboardPlayer
    {
        public bool IsEliminated { get; set; }
        public List<LeaderboardStatline> Statlines { get; set; } = [];
    }
}
