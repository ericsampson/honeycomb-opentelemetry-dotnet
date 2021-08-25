using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using netcoreapp21.Models;
using OpenTelemetry.Trace;

namespace netcoreapp21.Controllers
{
    public class HomeController : Controller
    {
        private readonly Tracer _tracer;
        public HomeController(Tracer tracer)
        {
            _tracer = tracer;
        }
        public async Task<string> Tester()
        {
            using var span = _tracer.StartActiveSpan("sleep");
            span.SetAttribute("delay_ms", 100);
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            return "test";
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
