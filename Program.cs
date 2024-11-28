using Microsoft.AspNetCore.Authentication.Cookies;
using Rotativa.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddRazorPages();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(1);
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, config =>
{
	config.ExpireTimeSpan = TimeSpan.FromMinutes(1);
	config.Events = new CookieAuthenticationEvents
	{
		OnRedirectToLogin = context =>
		{
			context.Response.Redirect("https://localhost:7262/");
			return Task.CompletedTask;
		}
	};
	config.AccessDeniedPath = "/Manage/ErrorAcceso";
});
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler("/Home/Error");
app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "schedule",
    pattern: "{controller=Schedule}/{action=Schedule}/{id?}");

IWebHostEnvironment env = app.Environment;
RotativaConfiguration.Setup(env.WebRootPath, "Rotativa");
app.Run();
