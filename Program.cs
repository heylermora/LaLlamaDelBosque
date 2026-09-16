using LaLlamaDelBosque.Interfaces;
using LaLlamaDelBosque.Services;
using LaLlamaDelBosque.Services.Scrapers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Rotativa.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddRazorPages();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IJsonRepository, JsonRepository>();
builder.Services.AddScoped<IScrapingService, ScrapingService>();
builder.Services.AddHttpClient("LotteryResults", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/json");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-CR,es;q=0.9,en;q=0.8");
});
builder.Services.AddTransient<IScraperStrategy>(services => new JpsNuevosTiemposScraper(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("LotteryResults"),
    services.GetRequiredService<TimeProvider>()));
builder.Services.AddTransient<IScraperStrategy>(services => new NicaraguaLotoDiariaScraper(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("LotteryResults"),
    services.GetRequiredService<TimeProvider>()));
builder.Services.AddTransient<IScraperStrategy>(services => new DominicanaLaPrimeraScraper(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("LotteryResults"),
    services.GetRequiredService<TimeProvider>()));
builder.Services.AddTransient<IScraperStrategy>(services => new HondurasLotoDiariaScraper(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("LotteryResults"),
    services.GetRequiredService<TimeProvider>()));

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

IWebHostEnvironment env = app.Environment;
string? configuredWebRootPath = env.WebRootPath;
string webRootPath;
if (string.IsNullOrWhiteSpace(configuredWebRootPath))
{
    webRootPath = Path.Combine(env.ContentRootPath, "wwwroot");
    if (!Directory.Exists(webRootPath))
    {
        throw new DirectoryNotFoundException($"Web root directory was not found at '{webRootPath}'. Ensure wwwroot is copied to the published output.");
    }

    env.WebRootPath = webRootPath;
}
else
{
    webRootPath = configuredWebRootPath;
}

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

string rotativaRelativePath = "Rotativa";
string rotativaPath = Path.Combine(webRootPath, rotativaRelativePath);

if (!Directory.Exists(rotativaPath))
{
    throw new DirectoryNotFoundException($"Rotativa wkhtmltopdf directory was not found at '{rotativaPath}'. Ensure wwwroot/Rotativa is copied to the published output.");
}

string wkhtmltopdfFileName = OperatingSystem.IsWindows() ? "wkhtmltopdf.exe" : "wkhtmltopdf";
string wkhtmltopdfPath = Path.Combine(rotativaPath, wkhtmltopdfFileName);
if (!File.Exists(wkhtmltopdfPath))
{
    throw new FileNotFoundException($"The platform-specific wkhtmltopdf executable '{wkhtmltopdfFileName}' was not found in '{rotativaPath}'. Ensure it is included in wwwroot/Rotativa and copied to the published output.", wkhtmltopdfPath);
}

RotativaConfiguration.Setup(webRootPath, rotativaRelativePath);
app.Run();
