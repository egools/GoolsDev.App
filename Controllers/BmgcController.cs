using GoolsDev.App.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace GoolsDev.App.Controllers
{
    public class BmgcController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public BmgcController(IWebHostEnvironment env) => _env = env;

        public IActionResult Index()
        {
            return Redirect("bmgc/playoffs");
        }

        [Route("bmgc/pickem")]
        public IActionResult Pickem()
        {
            var model = new PickemData();
            var rootDataPath = Path.Combine(_env.ContentRootPath, "Data", "Pickem");

            var chartDataPath = Path.Combine(rootDataPath, "pickem_chart_data_2026.json");
            model.ChartDataJson = System.IO.File.ReadAllText(chartDataPath);

            var parlayDataPath = Path.Combine(rootDataPath, "pickem_parlays_2026.json");
            var parlayResultText = System.IO.File.ReadAllText(parlayDataPath);
            var parlayResults = JsonSerializer.Deserialize<Dictionary<string, List<ParlayPick>>>(parlayResultText, jsonOptions);
            model.ParlayResults = parlayResults;

            var picksPath = Path.Combine(rootDataPath, "pickem_picks_2026.json");
            var picksText = System.IO.File.ReadAllText(picksPath);
            var picks = JsonSerializer.Deserialize<Dictionary<string, List<Pick>>>(picksText, jsonOptions);
            model.Picks = picks;

            var pickemScoreboardPath = Path.Combine(rootDataPath, "pickem_scores_2026.json");
            var pickemScoreboardText = System.IO.File.ReadAllText(pickemScoreboardPath);
            var pickemScoreboard = JsonSerializer.Deserialize<PickemScoreboard>(pickemScoreboardText, jsonOptions);
            model.Weeks = pickemScoreboard.Weeks;
            model.PickerNames = pickemScoreboard.Pickers;
            model.PropResults = pickemScoreboard.PropResults;
            model.PickemScores = pickemScoreboard.PickemScores;

            return View("Pickem", model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
