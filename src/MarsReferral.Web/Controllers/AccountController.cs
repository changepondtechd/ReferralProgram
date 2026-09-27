using Microsoft.AspNetCore.Mvc;
namespace MarsReferral.Web.Controllers;
public class AccountController : Controller
{
    [HttpGet("/")] public IActionResult Index() => View("~/Views/Gateway/Index.cshtml");
    [HttpGet("/Account/Login")] public IActionResult LegacyLogin() => Redirect("/");
    [HttpGet("/Account/Register")] public IActionResult LegacyRegister(string? code) => RedirectToAction("Register", "Onboarding", new {code});
    [HttpGet("/denied")] public IActionResult Denied() { Response.StatusCode = 403; return View("Notice", (object)"This portal needs its own demo sign-in. Return to the portal selector to continue."); }
    [Route("/error")] public IActionResult Error() { Response.StatusCode = 500; return View("Notice", (object)"Something went wrong. Return to the portal selector and try again."); }
}