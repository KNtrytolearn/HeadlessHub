using HeadlessHub.Core;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text.Json;

namespace HeadlessHub.WebApi;

public static class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        // ── Status ─────────────────────────────────────────────────────────────
        app.MapGet("/api/status", (ProfileManager pm) =>
            Results.Ok(new
            {
                platform    = PlatformHelper.OSName,
                architecture = PlatformHelper.ArchName,
                profileCount = pm.Profiles.Count,
                appCount    = pm.AppsAll.Count,
                runningApps = pm.AppsAll.Count(a => a.IsRunning),
                timestamp   = DateTime.UtcNow
            }));

        // ── Profiles ─────────────────────────────────────────────────────────
        app.MapGet("/api/profiles", (ProfileManager pm) =>
            Results.Ok(pm.Profiles.Select(p => new
            {
                p.Name,
                p.IsTaggedForStart,
                Apps = p.Apps.Select(kv => new
                {
                    Name            = kv.Key,
                    kv.Value.IsRequired,
                    kv.Value.TaggedForStart,
                    IsRunning       = kv.Value.App?.IsRunning ?? false
                })
            })));

        app.MapGet("/api/profiles/{name}", (string name, ProfileManager pm) =>
        {
            var profile = pm.Profiles.FirstOrDefault(p => p.Name == name);
            if (profile is null)
                return Results.NotFound(new { error = $"Profile '{name}' not found" });

            return Results.Ok(new
            {
                profile.Name,
                profile.IsTaggedForStart,
                Apps = profile.Apps.Select(kv => new
                {
                    Name               = kv.Key,
                    kv.Value.IsRequired,
                    kv.Value.TaggedForStart,
                    IsRunning          = kv.Value.App?.IsRunning ?? false,
                    kv.Value.RuntimeArguments
                })
            });
        });

        app.MapPost("/api/profiles/{name}/start", (string name, ProfileManager pm) =>
        {
            var profile = pm.Profiles.FirstOrDefault(p => p.Name == name);
            if (profile is null)
                return Results.NotFound(new { error = $"Profile '{name}' not found" });

            pm.RunProfile(name);
            return Results.Ok(new { message = $"Profile '{name}' started" });
        });

        app.MapPost("/api/profiles/{name}/stop", (string name, ProfileManager pm) =>
        {
            var profile = pm.Profiles.FirstOrDefault(p => p.Name == name);
            if (profile is null)
                return Results.NotFound(new { error = $"Profile '{name}' not found" });

            pm.StopProfile(name);
            return Results.Ok(new { message = $"Profile '{name}' stopped" });
        });

        // ── Apps ──────────────────────────────────────────────────────────────
        app.MapGet("/api/apps", (ProfileManager pm) =>
            Results.Ok(pm.AppsAll.Select(a => new
            {
                a.Name,
                a.CustomName,
                a.DescriptionShort,
                a.HelpUrl,
                IsInstalled    = a.IsInstalled(),
                IsRunning       = a.IsRunning,
                IsConfigurable  = a.IsConfigurable(),
                IsInstallable   = a.IsInstallable()
            })));

        app.MapGet("/api/apps/{name}", (string name, ProfileManager pm) =>
        {
            var app_ = pm.AppsAll.FirstOrDefault(a => a.Name == name);
            if (app_ is null)
                return Results.NotFound(new { error = $"App '{name}' not found" });

            return Results.Ok(new
            {
                app_.Name,
                app_.CustomName,
                app_.DescriptionShort,
                app_.DescriptionLong,
                app_.HelpUrl,
                app_.ChangelogUrl,
                IsInstalled    = app_.IsInstalled(),
                IsRunning       = app_.IsRunning,
                IsConfigurable  = app_.IsConfigurable(),
                IsInstallable   = app_.IsInstallable(),
                Configuration    = app_.Configuration != null ? new
                {
                    app_.Configuration.Prefix,
                    app_.Configuration.Delimiter,
                    Arguments = app_.Configuration.Arguments.Select(arg => new
                    {
                        arg.Name,
                        arg.NameHuman,
                        arg.Type,
                        arg.Required,
                        arg.Section,
                        arg.Description,
                        arg.Value,
                        arg.IsMulti
                    })
                } : null
            });
        });

        app.MapPost("/api/apps/{name}/download", async (string name, ProfileManager pm,
            [FromServices] LogBroadcastService lbs) =>
        {
            var app_ = pm.AppsAll.FirstOrDefault(a => a.Name == name);
            if (app_ is null)
                return Results.NotFound(new { error = $"App '{name}' not found" });

            if (app_ is not AppDownloadable downloadable)
                return Results.BadRequest(new { error = "App is not downloadable" });

            await lbs.BroadcastAsync($"Downloading {name}...", LogLevel.Information);

            var success = downloadable.Install();

            if (success)
            {
                pm.SaveApps();
                await lbs.BroadcastAsync($"Downloaded {name} successfully", LogLevel.Information);
            }
            else
            {
                await lbs.BroadcastAsync($"Download failed for {name}", LogLevel.Error);
            }

            return Results.Ok(new { message = $"App '{name}' processed", success });
        });

        app.MapPost("/api/apps/{name}/run", (string name, ProfileManager pm,
            [FromServices] LogBroadcastService lbs) =>
        {
            var app_ = pm.AppsAll.FirstOrDefault(a => a.Name == name);
            if (app_ is null)
                return Results.NotFound(new { error = $"App '{name}' not found" });

            var result = app_.Run();
            if (!result.Success)
            {
                lbs.BroadcastAsync($"Failed to start {name}: {result.Error}", LogLevel.Warning)
                    .Wait(TimeSpan.FromSeconds(1));
                return Results.Problem($"Failed to start: {result.Error}");
            }

            lbs.BroadcastAsync($"Started {name} (PID {result.ProcessId})", LogLevel.Information)
                .Wait(TimeSpan.FromSeconds(1));
            return Results.Ok(new { message = $"App '{name}' started", pid = result.ProcessId });
        });

        app.MapPost("/api/apps/{name}/stop", (string name, ProfileManager pm) =>
        {
            var app_ = pm.AppsAll.FirstOrDefault(a => a.Name == name);
            if (app_ is null)
                return Results.NotFound(new { error = $"App '{name}' not found" });

            app_.Close();
            return Results.Ok(new { message = $"App '{name}' stopped" });
        });

        app.MapPut("/api/apps/{name}/config", async (string name, HttpContext ctx, ProfileManager pm) =>
        {
            var app_ = pm.AppsAll.FirstOrDefault(a => a.Name == name);
            if (app_ is null)
                return Results.NotFound(new { error = $"App '{name}' not found" });

            if (app_.Configuration is null)
                return Results.BadRequest(new { error = "App is not configurable" });

            var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, string>>();
            if (body is null)
                return Results.BadRequest(new { error = "Invalid config body" });

            foreach (var (key, value) in body)
            {
                var arg = app_.Configuration.Arguments.FirstOrDefault(a => a.Name == key);
                if (arg != null) arg.Value = value;
            }

            pm.SaveApps();
            return Results.Ok(new { message = $"Config updated for '{name}'" });
        });

        // ── WebSocket log stream ───────────────────────────────────────────────
        app.MapGet("/ws/logs", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                return;
            }

            var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            var lbs = ctx.RequestServices.GetRequiredService<LogBroadcastService>();
            lbs.AddClient(socket);

            var buffer = new byte[1024];
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure, "Closing",
                            CancellationToken.None);
                        break;
                    }
                }
            }
            finally
            {
                lbs.RemoveClient(socket);
            }
        });

        // ── Frontend (served from wwwroot/index.html) ──────────────────────────
        app.MapGet("/", () => Results.Redirect("/index.html"));

        app.UseDefaultFiles();
        app.UseStaticFiles();
    }
}
