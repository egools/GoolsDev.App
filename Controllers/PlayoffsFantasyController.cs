using GoolsDev.App.Cosmos;
using GoolsDev.App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace GoolsDev.App.Controllers
{

    [ResponseCache(Duration = 60)]
    [Route("bmgc/playoffs")]
    public class PlayoffsFantasyController(
        ILogger<BmgcController> logger,
        ICosmosPlayerStats cosmosPlayerStats,
        ICosmosGames cosmosGames,
        IOptions<BmgcFantasyPlayoffsSettings> options,
        IMemoryCache memoryCache
            ) : Controller
    {
        private readonly ILogger<BmgcController> _logger = logger;
        private readonly ICosmosPlayerStats _cosmosPlayerStats = cosmosPlayerStats;
        private readonly ICosmosGames _cosmosGames = cosmosGames;
        private readonly BmgcFantasyPlayoffsSettings _settings = options.Value;
        private readonly IMemoryCache _memoryCache = memoryCache;

        public async Task<IActionResult> Index()
        {
            var fantasySeason = GetFantasySeason(_settings.CurrentNflSeason);
            var playerIDs = GetUniquePlayerIds(fantasySeason);
            var playerStatDocs = await _cosmosPlayerStats.GetPlayerStats(_settings.CurrentNflSeason, playerIDs);
            var gameDocs = await _cosmosGames.GetGames(_settings.CurrentNflSeason);
            return fantasySeason.ScoringType switch
            {
                ScoringType.Weekly => View(CreateStandardLeaderboard(fantasySeason, playerStatDocs, gameDocs, (int)_settings.CurrentSeasonType)),
                _ => View(CreateStandardLeaderboard(fantasySeason, playerStatDocs, gameDocs, (int)_settings.CurrentSeasonType))
            };
        }

        [Route("{name}")]
        public async Task<IActionResult> Team(string name)
        {
            var fantasySeason = GetFantasySeason(_settings.CurrentNflSeason);
            var teams = fantasySeason.Teams;
            if (!teams.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                return Redirect("/bmgc/playoffs");
            }

            var playerIDs = GetUniquePlayerIds(fantasySeason);
            var playerStatDocs = await _cosmosPlayerStats.GetPlayerStats(_settings.CurrentNflSeason, playerIDs);
            var gameDocs = await _cosmosGames.GetGames(_settings.CurrentNflSeason);
            var leaderboard = fantasySeason.ScoringType switch
            {
                ScoringType.Weekly => CreateStandardLeaderboard(fantasySeason, playerStatDocs, gameDocs, (int)_settings.CurrentSeasonType),
                _ => CreateStandardLeaderboard(fantasySeason, playerStatDocs, gameDocs, (int)_settings.CurrentSeasonType)
            };
            leaderboard.IsTeamPage = true;
            leaderboard.PlayerPageName = name;
            return View("Index", leaderboard);
        }

        [Route("leaders/playoffs")]
        public async Task<IActionResult> LeadingScorersPlayoffs() => await LeadingScorers(s => s.SeasonType == 3);

        [Route("leaders")]
        [Route("leaders/regular")]
        public async Task<IActionResult> LeadingScorersRegularSeason() => await LeadingScorers(s => s.SeasonType == 2 && s.Week == 99);

        private static IEnumerable<string> GetUniquePlayerIds(FantasySeason fantasySeason)
        {
            return fantasySeason.ScoringType switch
            {
                ScoringType.Standard => fantasySeason.Teams.SelectMany(t => t.StandardRoster.Values),
                ScoringType.Weekly => fantasySeason.Teams.SelectMany(t => t.WeeklyRoster.SelectMany(r => r.Value)).Distinct(),
                _ => []
            };
        }

        private async Task<IActionResult> LeadingScorers(Func<NflPlayerStatline, bool> filter)
        {
            var playerDocs = await _cosmosPlayerStats.GetAllPlayers(_settings.CurrentNflSeason);
            var fantasySeason = GetFantasySeason(_settings.CurrentNflSeason);
            var standardRosterMap = fantasySeason.Teams
                .SelectMany(t => t.StandardRoster.Where(p => !string.IsNullOrEmpty(p.Value))
                    .Select(p => new KeyValuePair<string, string>(p.Value, t.Name)))
                .ToDictionary();
            var players = new List<TopScorerPlayer>();
            foreach (var player in playerDocs)
            {
                standardRosterMap.TryGetValue(player.PlayerId, out var teamName);
                players.Add(new TopScorerPlayer
                {
                    FantasyTeam = teamName,
                    FirstName = player.FirstName,
                    LastName = player.LastName,
                    JerseyNumber = player.JerseyNumber,
                    Postion = player.Position,
                    TeamShortName = player.TeamShortName,
                    TotalPoints = player.Statlines.Where(filter).Sum(s => s.FantasyPoints)
                });
            }
            return View("LeadingScorers", players);
        }

        private FantasySeason GetFantasySeason(int year)
        {
            return _memoryCache.GetOrCreate(
                $"{year}Roster",
                cacheEntry =>
                {
                    cacheEntry.Priority = CacheItemPriority.NeverRemove;
                    cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60);
                    var text = System.IO.File.ReadAllText($"Data/playoffs_fantasy_roster_{year}.json");
                    return JsonSerializer.Deserialize<FantasySeason>(text, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                });
        }

        private static PlayoffsFantasyLeaderboard CreateStandardLeaderboard(
            FantasySeason fantasySeason,
            List<NflPlayerStatsDocument> playerDocs,
            List<NflGameDocument> gameDocs,
            int currentSeasonType)
        {
            var leaderboard = new PlayoffsFantasyLeaderboard
            {
                RosterPositions = fantasySeason.RosterPositions
            };

            var teamGames = new Dictionary<string, (NflGameDocument, NflGameTeam, NflGameTeam, bool)>();
            gameDocs.ForEach(g =>
            {
                if (g.HomeTeam.IsActive) teamGames.Add($"{g.HomeTeam.TeamId}.{g.Week}", (g, g.HomeTeam, g.AwayTeam, true));
                if (g.AwayTeam.IsActive) teamGames.Add($"{g.AwayTeam.TeamId}.{g.Week}", (g, g.AwayTeam, g.HomeTeam, false));
            });
            leaderboard.Weeks = gameDocs.Select(g => g.Week).Distinct().OrderBy(w => w);

            foreach (var fantasyTeam in fantasySeason.Teams)
            {
                var leaderboardTeam = new LeaderboardTeam
                {
                    Name = fantasyTeam.Name,
                    TotalPoints = 0
                };
                foreach (var (rosterPosition, id) in fantasyTeam.StandardRoster)
                {
                    var playerDoc = playerDocs.FirstOrDefault(p => p.PlayerId == id);
                    if (playerDoc is null) continue;

                    var player = new LeaderboardPlayer
                    {
                        FirstName = playerDoc.FirstName,
                        LastName = playerDoc.LastName,
                        TeamShortName = playerDoc.TeamShortName,
                        JerseyNumber = playerDoc.JerseyNumber,
                        Postion = playerDoc.Position,
                        RosterPosition = rosterPosition,
                    };

                    foreach (var week in leaderboard.Weeks)
                    {
                        var statline = playerDoc.Statlines.FirstOrDefault(s => s.Week == week && s.SeasonType == currentSeasonType);
                        var leaderboardStatline = statline is not null
                            ? new LeaderboardStatline(statline)
                            : new LeaderboardStatline()
                            {
                                Week = week,
                                SeasonType = currentSeasonType,
                                GameStatus = "No Game"
                            };
                        if (teamGames.TryGetValue($"{playerDoc.TeamId}.{leaderboardStatline.Week}", out var teamGame))
                        {
                            var (gameDoc, team, opponent, isHome) = teamGame;
                            var homeAway = isHome ? "vs" : "@";
                            if (DateTime.Now < gameDoc.Status.StartDate)
                            {
                                TimeZoneInfo easternStandardTime = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                                DateTimeOffset timeInEST = TimeZoneInfo.ConvertTime(gameDoc.Status.StartDate, easternStandardTime);
                                leaderboardStatline.GameStatus = $"{timeInEST:ddd h:mmtt \"EST\"} {homeAway} {opponent.ShortName}";
                            }
                            else if (gameDoc.Status.Complete)
                            {
                                var winLoss = team.Winner ? "W" : "L";
                                leaderboardStatline.GameStatus = $"Final ({winLoss}) {team.Score}-{opponent.Score} {homeAway} {opponent.ShortName}";
                                if (!team.Winner)
                                {
                                    player.IsEliminated = true;
                                }
                            }
                            else
                            {
                                var period = gameDoc.Status.Period switch
                                {
                                    1 => "1st",
                                    2 => "2nd",
                                    3 => "3rd",
                                    4 => "4th",
                                    > 4 => "OT",
                                    _ => string.Empty
                                };
                                leaderboardStatline.GameStatus = $"{team.Score}-{opponent.Score} {gameDoc.Status.Clock} {period} {homeAway} {opponent.ShortName}";
                            }

                            player.CurrentGameStatus = leaderboardStatline.GameStatus;
                            leaderboardStatline.HasGame = true;
                        }
                        player.Statlines.Add(leaderboardStatline);
                    }

                    leaderboardTeam.TotalPoints += player.Statlines.Sum(p => p.FantasyPoints);
                    leaderboardTeam.Players.Add(player);
                }

                leaderboard.FantasyTeams.Add(leaderboardTeam);
            }

            var rank = 1;
            foreach (var team in leaderboard.FantasyTeams.OrderByDescending(t => t.TotalPoints))
            {
                team.Rank = rank++;
            }

            return leaderboard;
        }

        private static PlayoffsFantasyLeaderboard CreateWeeklyLeaderboard(
           FantasySeason fantasySeason,
           List<NflPlayerStatsDocument> playerDocs,
           List<NflGameDocument> gameDocs,
           int currentSeasonType)
        {
            var leaderboard = new PlayoffsFantasyLeaderboard
            {
                RosterPositions = fantasySeason.RosterPositions
            };

            var teamGames = new Dictionary<string, (NflGameDocument, NflGameTeam, NflGameTeam, bool)>();
            gameDocs.ForEach(g =>
            {
                if (g.HomeTeam.IsActive) teamGames.Add($"{g.HomeTeam.TeamId}.{g.Week}", (g, g.HomeTeam, g.AwayTeam, true));
                if (g.AwayTeam.IsActive) teamGames.Add($"{g.AwayTeam.TeamId}.{g.Week}", (g, g.AwayTeam, g.HomeTeam, false));
            });
            leaderboard.Weeks = gameDocs.Select(g => g.Week).Distinct().OrderBy(w => w);

            foreach (var fantasyTeam in fantasySeason.Teams)
            {
                var leaderboardTeam = new LeaderboardTeam
                {
                    Name = fantasyTeam.Name,
                    TotalPoints = 0
                };
                foreach (var (rosterPosition, id) in fantasyTeam.StandardRoster)
                {
                    var playerDoc = playerDocs.FirstOrDefault(p => p.PlayerId == id);
                    if (playerDoc is null) continue;

                    var player = new LeaderboardPlayer
                    {
                        FirstName = playerDoc.FirstName,
                        LastName = playerDoc.LastName,
                        TeamShortName = playerDoc.TeamShortName,
                        JerseyNumber = playerDoc.JerseyNumber,
                        Postion = playerDoc.Position,
                        RosterPosition = rosterPosition,
                    };

                    foreach (var week in leaderboard.Weeks)
                    {
                        var statline = playerDoc.Statlines.FirstOrDefault(s => s.Week == week && s.SeasonType == currentSeasonType);
                        var leaderboardStatline = statline is not null
                            ? new LeaderboardStatline(statline)
                            : new LeaderboardStatline()
                            {
                                Week = week,
                                SeasonType = currentSeasonType,
                                GameStatus = "No Game"
                            };
                        if (teamGames.TryGetValue($"{playerDoc.TeamId}.{leaderboardStatline.Week}", out var teamGame))
                        {
                            var (gameDoc, team, opponent, isHome) = teamGame;
                            var homeAway = isHome ? "vs" : "@";
                            if (DateTime.Now < gameDoc.Status.StartDate)
                            {
                                TimeZoneInfo easternStandardTime = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                                DateTimeOffset timeInEST = TimeZoneInfo.ConvertTime(gameDoc.Status.StartDate, easternStandardTime);
                                leaderboardStatline.GameStatus = $"{timeInEST:ddd h:mmtt \"EST\"} {homeAway} {opponent.ShortName}";
                            }
                            else if (gameDoc.Status.Complete)
                            {
                                var winLoss = team.Winner ? "W" : "L";
                                leaderboardStatline.GameStatus = $"Final ({winLoss}) {team.Score}-{opponent.Score} {homeAway} {opponent.ShortName}";
                                if (!team.Winner)
                                {
                                    player.IsEliminated = true;
                                }
                            }
                            else
                            {
                                var period = gameDoc.Status.Period switch
                                {
                                    1 => "1st",
                                    2 => "2nd",
                                    3 => "3rd",
                                    4 => "4th",
                                    > 4 => "OT",
                                    _ => string.Empty
                                };
                                leaderboardStatline.GameStatus = $"{team.Score}-{opponent.Score} {gameDoc.Status.Clock} {period} {homeAway} {opponent.ShortName}";
                            }

                            player.CurrentGameStatus = leaderboardStatline.GameStatus;
                            leaderboardStatline.HasGame = true;
                        }
                        player.Statlines.Add(leaderboardStatline);
                    }

                    leaderboardTeam.TotalPoints += player.Statlines.Sum(p => p.FantasyPoints);
                    leaderboardTeam.Players.Add(player);
                }

                leaderboard.FantasyTeams.Add(leaderboardTeam);
            }

            var rank = 1;
            foreach (var team in leaderboard.FantasyTeams.OrderByDescending(t => t.TotalPoints))
            {
                team.Rank = rank++;
            }

            return leaderboard;
        }
    }
}
