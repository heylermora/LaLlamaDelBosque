using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace LaLlamaDelBosque.Controllers;

public class AuthController: Controller
{
	private AuthModel _auth;

	public AuthController()
	{
		_auth = GetAuth();
	}

	public IActionResult Index()
	{
		return View();
	}

	[HttpPost]
	public async Task<IActionResult> Index(string email, string password)
	{

		var auth = LogIn(email, password);
		if(auth == null)
		{
			ViewData["MENSAJE"] = "No tienes las credenciales correctas.";
			return View();
		}
		else
		{
			ClaimsIdentity identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
			Claim claimUserName = new Claim(ClaimTypes.Name, auth.Name);
			Claim claimUserId = new Claim("Id", auth.Id.ToString());
			Claim claimUserEmail = new Claim("Email", auth.Email);

			identity.AddClaim(claimUserName);
			identity.AddClaim(claimUserId);
			identity.AddClaim(claimUserEmail);

			ClaimsPrincipal user = new ClaimsPrincipal(identity);
			await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, user, new AuthenticationProperties
			{
				ExpiresUtc = DateTime.Now.AddMinutes(120)
			});

			return RedirectToAction("Index", "Home");
		}

	}

	public async Task<IActionResult> LogOut()
	{
		await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
		return RedirectToAction("Index", "Auth");
	}

	private AuthModel? LogIn(string email, string password)
	{
		var auth = _auth.Email == email && _auth.Password == password ? _auth : null;

		return auth;
	}

	private AuthModel GetAuth()
	{
		var auth = JsonFile.Read<AuthModel>("Auth", new AuthModel());
		return auth;
	}
}