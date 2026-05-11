using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http; // Thêm dòng này
using Microsoft.AspNetCore.Mvc.Filters; // Thêm dòng này
using temp_room_esp32.Models;

namespace temp_room_esp32.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    // Bắt buộc đăng nhập cho các action trừ Login và Error
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var action = context.ActionDescriptor.RouteValues["action"];
        if (action != "Login" && action != "Error")
        {
            if (HttpContext.Session.GetString("IsAdmin") != "true")
            {
                context.Result = RedirectToAction("Login");
            }
        }
        base.OnActionExecuting(context);
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult History()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Login(string username, string password)
    {
        if (username == "admin" && password == "123456")
        {
            HttpContext.Session.SetString("IsAdmin", "true");
            return RedirectToAction("Index");
        }
        ViewBag.Error = "Sai tài khoản hoặc mật khẩu";
        return View();
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Remove("IsAdmin");
        return RedirectToAction("Login");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
    private readonly MqttDataService _mqtt;

    
}
