using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MVC.Services;
using project.Models;
using project.Services;

namespace project.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AuthServices _auth;

    private readonly RabbitMqService _mq;
        private readonly IChatService _chat;
    public HomeController(ILogger<HomeController> logger, AuthServices auth,RabbitMqService mq,IChatService chat)
    {
        _logger = logger;
        _auth = auth;
        _mq = mq;
        _chat = chat;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<JsonResult> Logins([FromForm] Vm_login login)
    {
        if (login == null || string.IsNullOrEmpty(login.EmpMail) || string.IsNullOrEmpty(login.EmpPassword))
            return Json(new { message = "Invalid request", success = false });

        var status = await _auth.Login(login);
        if (status)
        {
            string role = await _auth.GetByEmail(login.EmpMail);
            string statuses = await _auth.GetByEmailStatus(login.EmpMail);
            int ids = await _auth.GetByEmailId(login.EmpMail);

            HttpContext.Session.SetString("Role", role);
            HttpContext.Session.SetString("Statuses", statuses);
            HttpContext.Session.SetString("Mail", login.EmpMail);
            HttpContext.Session.SetString("Ids", Convert.ToString(ids));
            return Json(new { message = "Login successfully", success = true, role = role, statuses = statuses , ids = ids });
        } 

        return Json(new { message = "Invalid Credential", success = false, role = "" });
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }


    [HttpPost]
    public async Task<JsonResult> Register(EmployeeModel emp)
    {
        // if (!ModelState.IsValid)
        // {
        //     // Collect model validation errors
        //     var errors = ModelState.Values.SelectMany(v => v.Errors)
        //                                   .Select(e => e.ErrorMessage)
        //                                   .ToList();

        //     return Json(new
        //     {
        //         success = false,
        //         message = "Validation failed",
        //         errors
        //     });
        // }



        // Check if email already exists
        if (await _auth.IsEmailExistsAsync(emp.EmpMail))
        {
            return Json(new
            {
                success = false,
                message = "Email already exists. Please try another."
            });
        }

        bool isRegistered = await _auth.RegisterAsync(emp);

        if (isRegistered)
        {
            string message = $"New user registered: {emp.EmpName} ({emp.EmpMail}) at {DateTime.Now}";
                    _mq.SendToUser("System", "admin", message);
                    _chat.SendToUser("System", "admin", message);
            return Json(new
            {
                success = true,
                message = "Registration successful!"
            });
        }
        else
        {
            return Json(new
            {
                success = false,
                message = "Registration failed due to server error."
            });
        }
    }

    [HttpGet]
    public IActionResult Logout()
    {
        // Clear session
        HttpContext.Session.Clear();

        // Redirect to login or home page
        return RedirectToAction("Index");
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
