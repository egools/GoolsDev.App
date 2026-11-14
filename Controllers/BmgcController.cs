using GoolsDev.App.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace GoolsDev.App.Controllers
{
    public class BmgcController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly string _rootDataPath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public BmgcController(IWebHostEnvironment env)
        {
            _env = env;
            _rootDataPath = Path.Combine(_env.ContentRootPath, "Data", "Pickem");
        }

        public IActionResult Index()
        {
            return Redirect("bmgc/playoffs");
        }

        [Route("bmgc/pickem")]
        [OutputCache(PolicyName = "Expire60m")]
        public IActionResult Pickem()
        {
            var model = new PickemData();
            

            var chartDataPath = Path.Combine(_rootDataPath, "pickem_chart_data_2026.json");
            model.ChartDataJson = System.IO.File.ReadAllText(chartDataPath);

            var parlayResults = GetDataFile<Dictionary<string, List<ParlayPick>>>("pickem_parlays_2026.json");
            model.ParlayResults = parlayResults;

            var picks = GetDataFile<Dictionary<string, List<Pick>>>("pickem_picks_2026.json");
            model.Picks = picks;

            var pickemScoreboard =GetDataFile<PickemScoreboard>("pickem_scores_2026.json");
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

        private T GetDataFile<T>(string fileName)
        {
            var filePath = Path.Combine(_rootDataPath, fileName);
            var fileText = System.IO.File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(fileText, _jsonOptions);
        }
    }
}
