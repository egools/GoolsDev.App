using GoolsDev.App.Cosmos;
using GoolsDev.App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace GoolsDev.App.Controllers
{

    [ResponseCache(Duration = 60)]
    [Route("bmgc/playoffs")]
    public class PlayoffsFantasyController : Controller
    {
        private readonly ILogger<BmgcController> _logger;
        private readonly ICosmosPlayerStats _cosmosPlayerStats;
        private readonly ICosmosGames _cosmosGames;
        private readonly IMemoryCache _memoryCache;
        private const int _year = 2024;

        public PlayoffsFantasyController(
            ILogger<BmgcController> logger,
            ICosmosPlayerStats cosmosPlayerStats,
            ICosmosGames cosmosGames,
            IMemoryCache memoryCache
            )
        {
            _logger = logger;
            _cosmosPlayerStats = cosmosPlayerStats;
            _cosmosGames = cosmosGames;
            _memoryCache = memoryCache;
        }

        public async Task<IActionResult> Index()
        {
            var teams = GetFantasyTeams(_year);
            var playerIDs = teams.SelectMany(t => t.Players.Values);
            var playerStatDocs = await _cosmosPlayerStats.GetPlayerStats(_year, playerIDs);
            var gameDocs = await _cosmosGames.GetGames(_year);
            return View(CreateLeaderboard(teams, playerStatDocs, gameDocs));
        }

        [Route("leaders")]
        public async Task<IActionResult> LeadingScorers()
        {
            var players = await _cosmosPlayerStats.GetAllPlayers(_year);
            return View(players);
        }

        private List<PlayoffFantasyTeam> GetFantasyTeams(int year)
        {
            return _memoryCache.GetOrCreate(
                "2024Roster",
                cacheEntry =>
                {
                    cacheEntry.Priority = CacheItemPriority.NeverRemove;
                    cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(60);
                    var text = System.IO.File.ReadAllText($"Data/playoffs_fantasy_roster_{year}.json");
                    return JsonSerializer.Deserialize<List<PlayoffFantasyTeam>>(text, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                });
        }

        private PlayoffsFantasyLeaderboard CreateLeaderboard(
            List<PlayoffFantasyTeam> fantasyTeams,
            List<NflPlayerStatsDocument> playerDocs,
            List<NflGameDocument> gameDocs)
        {
            var leaderboard = new PlayoffsFantasyLeaderboard();

            var teamGames = new Dictionary<string, (NflGameDocument, NflGameTeam, NflGameTeam, bool)>();
            gameDocs.ForEach(g =>
            {
                if (g.HomeTeam.IsActive) teamGames.Add($"{g.HomeTeam.TeamId}.{g.Week}", (g, g.HomeTeam, g.AwayTeam, true));
                if (g.AwayTeam.IsActive) teamGames.Add($"{g.AwayTeam.TeamId}.{g.Week}", (g, g.AwayTeam, g.HomeTeam, false));
            });
            leaderboard.Weeks = gameDocs.Select(g => g.Week).Distinct().OrderBy(w => w);

            foreach (var fantasyTeam in fantasyTeams)
            {
                var leaderboardTeam = new LeaderboardTeam
                {
                    Name = fantasyTeam.Name,
                    TotalPoints = 0
                };
                foreach (var (rosterPosition, id) in fantasyTeam.Players)
                {
                    var playerDoc = playerDocs.FirstOrDefault(p => p.PlayerId == id);
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
                        var statline = playerDoc.Statlines.FirstOrDefault(s => s.Week == week && s.SeasonType == 3);
                        var leaderBoardStatline = statline is not null
                            ? new LeaderboardStatline(statline)
                            : new LeaderboardStatline()
                            {
                                Week = week,
                                SeasonType = 3,
                                GameStatus = "No Game"
                            };
                        if (teamGames.TryGetValue($"{playerDoc.TeamId}.{leaderBoardStatline.Week}", out var teamGame))
                        {
                            var (gameDoc, team, opponent, isHome) = teamGame;
                            var homeAway = isHome ? "vs" : "@";
                            if (DateTime.Now < gameDoc.Status.StartDate)
                            {
                                TimeZoneInfo easternStandardTime = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                                DateTimeOffset timeInEST = TimeZoneInfo.ConvertTime(gameDoc.Status.StartDate, easternStandardTime);
                                leaderBoardStatline.GameStatus = $"{gameDoc.Status.StartDate:ddd h:mm tt \"EST\"} {homeAway} {opponent.ShortName}";
                            }
                            else if (gameDoc.Status.Complete)
                            {
                                var winLoss = team.Winner ? "W" : "L";
                                leaderBoardStatline.GameStatus = $"Final {winLoss} {team.Score}-{opponent.Score} {homeAway} {opponent.ShortName}";
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
                                leaderBoardStatline.GameStatus = $"{gameDoc.Status.Clock} {team.Score}-{opponent.Score} {homeAway} {opponent.ShortName}";
                            }
                            player.CurrentGameStatus = leaderBoardStatline.GameStatus;
                        }
                        player.Statlines.Add(leaderBoardStatline);
                    }

                    leaderboardTeam.TotalPoints += player.Statlines.Sum(p => p.FantasyPoints);
                    leaderboardTeam.Players.Add(player);
                }

                leaderboard.FantasyTeams.Add(leaderboardTeam);
            }

            return leaderboard;
        }
    }

    public class PlayoffFantasyTeam
    {
        public string Name { get; set; }
        public Dictionary<string, string> Players { get; set; }
    }
}
