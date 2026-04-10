using Microsoft.AspNetCore.StaticFiles;
using HeadlessHub.Core;
using HeadlessHub.WebApi;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Kestrel: HTTP only, bind all interfaces
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(5000));

// ProfileManager: singleton, owns all app/profile data for the lifetime of the app
builder.Services.AddSingleton<ProfileManager>();

// LogBroadcastService: registered as BOTH singleton (so endpoints can inject it)
// and IHostedService (so the host calls StartAsync/StopAsync).
builder.Services.AddSingleton<LogBroadcastService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LogBroadcastService>());

// Static files (Web UI) - wwwroot/ is auto-included by Microsoft.NET.Sdk.Web
builder.Services.AddDefaultFiles();
builder.Services.AddStaticFiles();

var app = builder.Build();

// Banner
Console.WriteLine("+==========================================+");
Console.WriteLine("|       HeadlessHub for autodarts.io      |");
Console.WriteLine("|     RK3528 Darts Extension Manager      |");
Console.WriteLine("+==========================================+");
Console.WriteLine();

// Init
var pm = app.Services.GetRequiredService<ProfileManager>();
pm.LoadAppsAndProfiles();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("HeadlessHub started");
logger.LogInformation("Platform: {Platform} ({Arch}) | {AppCount} apps, {ProfileCount} profiles",
    PlatformHelper.OSName, PlatformHelper.ArchName,
    pm.AppsAll.Count, pm.Profiles.Count);
logger.LogInformation("Web UI: http://0.0.0.0:5000");

// Routes
app.MapEndpoints();

await app.RunAsync();
