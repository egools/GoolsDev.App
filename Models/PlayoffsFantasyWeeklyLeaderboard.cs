using GoolsDev.App.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GoolsDev.App.Models
{
    public class PlayoffsFantasyWeeklyLeaderboard : PlayoffsFantasyLeaderboard
    {
        public override string ViewName => "WeeklyLeaderboard";
        public List<WeeklyLeaderboardTeam> FantasyTeams { get; set; } = [];

        public PlayoffsFantasyWeeklyLeaderboard(
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
            Weeks = gameDocs.Select(g => g.Week).Distinct().OrderBy(w => w);

            foreach (var fantasyTeam in fantasySeason.Teams)
            {
                var leaderboardTeam = new WeeklyLeaderboardTeam
                {
                    Name = fantasyTeam.Name,
                    TotalPoints = 0
                };

                foreach (var week in Weeks)
                {
                    var weeklyRoster = new List<WeeklyLeaderboardPlayer>();
                    leaderboardTeam.WeeklyRosters.TryAdd(week, weeklyRoster);

                    if (!fantasyTeam.WeeklyRosters.TryGetValue(week, out var teamWeek)) continue;

                    foreach (var playerId in teamWeek)
                    {
                        var playerDoc = playerDocs.FirstOrDefault(p => p.PlayerId == playerId);
                        if (playerDoc is null) continue;

                        var player = new WeeklyLeaderboardPlayer
                        {
                            FirstName = playerDoc.FirstName,
                            LastName = playerDoc.LastName,
                            TeamShortName = playerDoc.TeamShortName,
                            JerseyNumber = playerDoc.JerseyNumber,
                            Position = playerDoc.Position,
                            RosterPosition = "BN"
                        };

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
                            var team = playerGameDoc.HomeTeam.TeamId == playerDoc.TeamId
                                 ? playerGameDoc.HomeTeam
                                 : playerGameDoc.AwayTeam;
                            leaderboardStatline.GameStatus = team.GameStatus;
                            if (playerGameDoc.Status.Complete && !team.Winner)
                            {
                                player.IsEliminated = true;
                            }
                        }
                        else
                        {
                            player.IsEliminated = true;
                        }

                        player.Statline = leaderboardStatline;
                        weeklyRoster.Add(player);
                    }

                    foreach (var position in RosterPositions)
                    {
                        Func<WeeklyLeaderboardPlayer, bool> filter = position == "FL"
                            ? p => (p.Position == "RB" || p.Position == "WR" || p.Position == "TE") && p.RosterPosition == "BN"
                            : p => p.Position == position && p.RosterPosition == "BN";

                        var bestBall = weeklyRoster.Where(filter).MaxBy(p => p.Statline.FantasyPoints);
                        if (bestBall is not null)
                        {
                            bestBall.RosterPosition = position;
                        }
                    }

                    leaderboardTeam.TotalPoints += 
                        weeklyRoster
                        .Where(p => p.RosterPosition != "BN")
                        .Sum(p => p.Statline.FantasyPoints);
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

    public class WeeklyLeaderboardTeam : LeaderboardTeam
    {
        public Dictionary<int, List<WeeklyLeaderboardPlayer>> WeeklyRosters { get; set; } = [];
    }

    public class WeeklyLeaderboardPlayer : LeaderboardPlayer
    {
        public LeaderboardStatline Statline { get; set; }
    }

}
