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
    public class PlayoffsFantasyController : Controller
    {
        private readonly ILogger<BmgcController> _logger;
        private readonly ICosmosPlayerStats _cosmosPlayerStats;
        private readonly ICosmosGames _cosmosGames;
        private readonly BmgcFantasyPlayoffsSettings _settings;
        private readonly IMemoryCache _memoryCache;

        public PlayoffsFantasyController(
            ILogger<BmgcController> logger,
            ICosmosPlayerStats cosmosPlayerStats,
            ICosmosGames cosmosGames,
            IOptions<BmgcFantasyPlayoffsSettings> options,
            IMemoryCache memoryCache
            )
        {
            _logger = logger;
            _cosmosPlayerStats = cosmosPlayerStats;
            _cosmosGames = cosmosGames;
            _settings = options.Value;
            _memoryCache = memoryCache;
        }

        public async Task<IActionResult> Index()
        {
            var teams = GetFantasyTeams(_settings.CurrentNflSeason);
            var playerIDs = teams.SelectMany(t => t.Players.Values);
            var playerStatDocs = await _cosmosPlayerStats.GetPlayerStats(_settings.CurrentNflSeason, playerIDs);
            var gameDocs = await _cosmosGames.GetGames(_settings.CurrentNflSeason);
            return View(CreateLeaderboard(teams, playerStatDocs, gameDocs));
        }

        [Route("{name}")]
        public async Task<IActionResult> Team(string name)
        {
            var teams = GetFantasyTeams(_settings.CurrentNflSeason);
            if (!teams.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                return Redirect("/bmgc/playoffs");
            }

            var playerIDs = teams.SelectMany(t => t.Players.Values);
            var playerStatDocs = await _cosmosPlayerStats.GetPlayerStats(_settings.CurrentNflSeason, playerIDs);
            var gameDocs = await _cosmosGames.GetGames(_settings.CurrentNflSeason);
            var leaderboard = CreateLeaderboard(teams, playerStatDocs, gameDocs);
            leaderboard.IsTeamPage = true;
            leaderboard.PlayerPageName = name;
            return View("Index", leaderboard);
        }

        [Route("leaders")]
        public async Task<IActionResult> LeadingScorers()
        {
            var playerDocs = await _cosmosPlayerStats.GetAllPlayers(_settings.CurrentNflSeason);
            var teams = GetFantasyTeams(_settings.CurrentNflSeason);
            var model = CreatePlayerLeaderboard(teams, playerDocs);
            return View(model);
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

        private List<TopScorerPlayer> CreatePlayerLeaderboard(
            List<PlayoffFantasyTeam> fantasyTeams,
            List<NflPlayerStatsDocument> playerDocs)
        {
            var rosterMap = fantasyTeams
                .SelectMany(t => t.Players
                    .Select(p => new KeyValuePair<string, string>(p.Value, t.Name)))
                .ToDictionary();
            var players = new List<TopScorerPlayer>();
            foreach (var player in playerDocs)
            {
                rosterMap.TryGetValue(player.PlayerId, out var teamName);
                players.Add(new TopScorerPlayer
                {
                    FantasyTeam = teamName,
                    FirstName = player.FirstName,
                    LastName = player.LastName,
                    JerseyNumber = player.JerseyNumber,
                    Postion = player.Position,
                    TeamShortName = player.TeamShortName,
                    TotalPoints = player.Statlines.Where(s => s.SeasonType == 3).Sum(s => s.FantasyPoints)
                });
            }
            return players;
        }

        private PlayoffsFantasyLeaderboard CreateLeaderboard(
            List<PlayoffFantasyTeam> fantasyTeams,
            List<NflPlayerStatsDocument> playerDocs,
            List<NflGameDocument> gameDocs)
        {
            var leaderboard = new PlayoffsFantasyLeaderboard();

            leaderboard.RosterPositions = ["QB", "RB", "WR1", "WR2", "TE", "FL1", "FL2"];
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
                        var statline = playerDoc.Statlines.FirstOrDefault(s => s.Week == week && s.SeasonType == _settings.CurrentSeasonType);
                        var leaderboardStatline = statline is not null
                            ? new LeaderboardStatline(statline)
                            : new LeaderboardStatline()
                            {
                                Week = week,
                                SeasonType = _settings.CurrentSeasonType,
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

    public class PlayoffFantasyTeam
    {
        public string Name { get; set; }
        public Dictionary<string, string> Players { get; set; }
    }
}
