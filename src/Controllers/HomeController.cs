using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using proset.Models;

namespace proset.Controllers;

public class HomeController : Controller {
    private readonly ILogger<HomeController> _logger;
    private readonly SqlContext _context;

    public HomeController(ILogger<HomeController> logger, SqlContext context) {
        _logger = logger;
        _context = context;
    }

    public IActionResult Index() { return View(); }

    public IActionResult Privacy() { return View(); }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() {
        return View(new ErrorViewModel { Reason = "idk man"} );// Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
