using GoolsDev.App.Cosmos;
using GoolsDev.App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
            PlayoffsFantasyLeaderboard leaderboard = fantasySeason.ScoringType switch
            {
                ScoringType.Weekly => new PlayoffsFantasyWeeklyLeaderboard(fantasySeason.RosterPositions, (int)_settings.CurrentSeasonType),
                _ => new PlayoffsFantasyStandardLeaderboard(fantasySeason.RosterPositions, (int)_settings.CurrentSeasonType)
            };

            leaderboard.Build(fantasySeason, playerStatDocs, gameDocs);

            return View(leaderboard.ViewName, leaderboard);
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
            PlayoffsFantasyLeaderboard leaderboard = fantasySeason.ScoringType switch
            {
                ScoringType.Weekly => new PlayoffsFantasyWeeklyLeaderboard(fantasySeason.RosterPositions, (int)_settings.CurrentSeasonType),
                _ => new PlayoffsFantasyStandardLeaderboard(fantasySeason.RosterPositions, (int)_settings.CurrentSeasonType)
            };

            leaderboard.IsTeamPage = true;
            leaderboard.PlayerPageName = name;
            leaderboard.Build(fantasySeason, playerStatDocs, gameDocs);

            return View(leaderboard.ViewName, leaderboard);
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
                ScoringType.Weekly => fantasySeason.Teams.SelectMany(t => t.WeeklyRosters.SelectMany(r => r.Value)).Distinct(),
                _ => []
            };
        }

        private async Task<IActionResult> LeadingScorers(Func<NflPlayerStatline, bool> filter)
        {
            var playerDocs = await _cosmosPlayerStats.GetAllPlayers(_settings.CurrentNflSeason);
            var fantasySeason = GetFantasySeason(_settings.CurrentNflSeason);
            var teamName = string.Empty;
            Dictionary<string, string> standardRosterMap = [];
            if (fantasySeason.ScoringType == ScoringType.Standard)
            {
                standardRosterMap = fantasySeason.Teams
                    .SelectMany(t => t.StandardRoster.Where(p => !string.IsNullOrEmpty(p.Value))
                        .Select(p => new KeyValuePair<string, string>(p.Value, t.Name)))
                    .ToDictionary();
            }
            var players = new List<TopScorerPlayer>();
            foreach (var player in playerDocs)
            {
                standardRosterMap.TryGetValue(player.PlayerId, out teamName);
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
    }
}
