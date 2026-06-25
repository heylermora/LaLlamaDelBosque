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
string rotativaRelativePath = Path.Combine("wwwroot", "Rotativa");
string rotativaPath = Path.Combine(env.ContentRootPath, rotativaRelativePath);

if (!Directory.Exists(rotativaPath))
{
    throw new DirectoryNotFoundException($"Rotativa wkhtmltopdf directory was not found at '{rotativaPath}'. Ensure wwwroot/Rotativa is copied to the published output.");
}

string wkhtmltopdfPath = Path.Combine(rotativaPath, OperatingSystem.IsWindows() ? "wkhtmltopdf.exe" : "wkhtmltopdf");
if (!File.Exists(wkhtmltopdfPath))
{
    string windowsWkhtmltopdfPath = Path.Combine(rotativaPath, "wkhtmltopdf.exe");
    if (!File.Exists(windowsWkhtmltopdfPath))
    {
        throw new FileNotFoundException($"wkhtmltopdf was not found in '{rotativaPath}'. Ensure the wkhtmltopdf executable is included in wwwroot/Rotativa and copied to the published output.", wkhtmltopdfPath);
    }
}

RotativaConfiguration.Setup(env.ContentRootPath, rotativaRelativePath);
app.Run();
