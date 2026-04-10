using HeadlessHub.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HeadlessHub.Core;

/// <summary>
/// Central manager for apps and profiles. Loaded once at startup.
/// All apps share the same <see cref="ILogger"/> injected at construction time,
/// eliminating the need to set it manually after deserialisation.
/// </summary>
public class ProfileManager
{
    private readonly ILogger<ProfileManager> _logger;

    private readonly string _appsFile    = Path.Combine(Helper.GetAppBasePath(), "data", "apps-downloadable.json");
    private readonly string _profilesFile = Path.Combine(Helper.GetAppBasePath(), "data", "profiles.json");

    public List<AppDownloadable> AppsDownloadable { get; } = new();
    public List<AppBase>         AppsAll          { get; } = new();
    public List<Profile>         Profiles          { get; } = new();

    public ProfileManager(ILogger<ProfileManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var dir = Path.GetDirectoryName(_appsFile);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    // ── Load / Save ───────────────────────────────────────────────────────────

    public void LoadAppsAndProfiles()
    {
        AppsDownloadable.Clear();
        AppsAll.Clear();
        Profiles.Clear();

        // ── Apps ──
        if (File.Exists(_appsFile))
        {
            var loaded = JsonConvert.DeserializeObject<List<AppDownloadable>>(
                File.ReadAllText(_appsFile));

            if (loaded != null)
            {
                foreach (var app in loaded) app.Logger = _logger;
                AppsDownloadable.AddRange(loaded);
                AppsAll.AddRange(loaded);
            }
        }
        else
        {
            _CreateDefaultApps();
        }

        // ── Profiles ──
        if (File.Exists(_profilesFile))
        {
            var loaded = JsonConvert.DeserializeObject<List<Profile>>(
                File.ReadAllText(_profilesFile));

            if (loaded != null)
            {
                Profiles.AddRange(loaded);
                _BindAppsToProfiles();
            }
        }
        else
        {
            _CreateDefaultProfiles();
        }

        _logger.LogInformation("Loaded {Profiles} profiles, {Apps} apps",
            Profiles.Count, AppsAll.Count);
    }

    public void SaveApps()
    {
        var settings = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling    = NullValueHandling.Ignore,
            Formatting           = Formatting.Indented
        };

        File.WriteAllText(_appsFile,    JsonConvert.SerializeObject(AppsDownloadable, settings));
        File.WriteAllText(_profilesFile, JsonConvert.SerializeObject(Profiles,          settings));
    }

    // ── Profile operations ────────────────────────────────────────────────────

    public void RunProfile(string profileName)
    {
        var profile = Profiles.FirstOrDefault(p => p.Name == profileName);
        if (profile == null)
        {
            _logger.LogWarning("Profile '{Name}' not found", profileName);
            return;
        }

        _logger.LogInformation("Starting profile: {Name}", profileName);

        foreach (var (appName, state) in profile.Apps)
        {
            if (!state.TaggedForStart && !state.IsRequired) continue;

            var result = state.App?.Run(state.RuntimeArguments);
            if (result is { Success: false })
                _logger.LogWarning("Failed to start {App}: {Error}", appName, result.Error);
        }
    }

    public void StopProfile(string profileName)
    {
        var profile = Profiles.FirstOrDefault(p => p.Name == profileName);
        if (profile == null) return;

        _logger.LogInformation("Stopping profile: {Name}", profileName);

        foreach (var (_, state) in profile.Apps)
        {
            try { state.App?.Close(); }
            catch (Exception ex) { _logger.LogError(ex, "Error stopping app"); }
        }
    }

    public void StopAllApps()
    {
        foreach (var app in AppsAll)
        {
            try { app.Close(); }
            catch { }
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Binds every ProfileState.App to the matching AppBase instance in AppsAll.
    /// Called after deserialising profiles from disk.
    /// </summary>
    private void _BindAppsToProfiles()
    {
        foreach (var profile in Profiles)
        {
            foreach (var (appName, state) in profile.Apps)
            {
                var app = AppsAll.FirstOrDefault(a => a.Name == appName);
                if (app != null)
                    state.Bind(app);
                else
                    _logger.LogWarning(
                        "Profile '{Profile}' references unknown app '{App}'", profile.Name, appName);
            }
        }
    }

    private void _CreateDefaultApps()
    {
        var callerUrl = PlatformHelper.GetCallerDownloadUrl();
        if (!string.IsNullOrEmpty(callerUrl))
        {
            AppsDownloadable.Add(new AppDownloadable(
                downloadUrl: callerUrl,
                name: "darts-caller",
                descriptionShort: "Calls out thrown points",
                helpUrl: "https://github.com/lbormann/darts-caller",
                changelogUrl: "https://raw.githubusercontent.com/lbormann/darts-caller/master/CHANGELOG.md",
                configuration: new Configuration(
                    prefix: "-", delimiter: " ",
                    arguments: new List<Argument>
                    {
                        new("U",   "string",         true,  section: "Autodarts", nameHuman: "-U / --autodarts_email"),
                        new("P",   "password",        true,  section: "Autodarts", nameHuman: "-P / --autodarts_password"),
                        new("B",   "string",          true,  section: "Autodarts", nameHuman: "-B / --autodarts_board_id"),
                        new("M",   "path",            true,  section: "Media",     nameHuman: "-M / --media_path"),
                        new("V",   "float[0.0..1.0]", false, section: "Media",     nameHuman: "-V / --caller_volume"),
                        new("LPB", "bool",            false, section: "Calls",     nameHuman: "-LPB / --local_playback",
                            valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" }),
                        new("DEB", "bool",            false, section: "Service",   nameHuman: "-DEB / --debug",
                            valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" })
                    }
                )
            ));
        }

        var wledUrl = PlatformHelper.GetWledDownloadUrl();
        if (!string.IsNullOrEmpty(wledUrl))
        {
            AppsDownloadable.Add(new AppDownloadable(
                downloadUrl: wledUrl,
                name: "darts-wled",
                descriptionShort: "Controls WLED installations by autodarts-events",
                helpUrl: "https://github.com/lbormann/darts-wled",
                changelogUrl: "https://raw.githubusercontent.com/lbormann/darts-wled/master/CHANGELOG.md",
                configuration: new Configuration(
                    prefix: "-", delimiter: " ",
                    arguments: new List<Argument>
                    {
                        new("WEPS", "string",  true,  isMulti: true, section: "Service", nameHuman: "-WEPS / --wled_endpoints"),
                        new("BRI",  "int[1..255]", false, section: "Service", nameHuman: "-BRI / --effect_brightness"),
                        new("IDE",  "string",  false, isMulti: true, section: "Service", nameHuman: "-IDE / --idle_effect"),
                        new("G",    "string",  false, isMulti: true, section: "Effects", nameHuman: "-G / --game_won_effects"),
                        new("DEB",  "bool",    false, section: "Service", nameHuman: "-DEB / --debug",
                            valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" })
                    }
                )
            ));
        }

        var pixelitUrl = PlatformHelper.GetPixelitDownloadUrl();
        if (!string.IsNullOrEmpty(pixelitUrl))
        {
            AppsDownloadable.Add(new AppDownloadable(
                downloadUrl: pixelitUrl,
                name: "darts-pixelit",
                descriptionShort: "Controls PIXELIT installations by autodarts-events",
                helpUrl: "https://github.com/lbormann/darts-pixelit",
                changelogUrl: "https://raw.githubusercontent.com/lbormann/darts-pixelit/main/CHANGELOG.md",
                configuration: new Configuration(
                    prefix: "-", delimiter: " ",
                    arguments: new List<Argument>
                    {
                        new("PEPS", "string",  true,  isMulti: true, section: "PixelIt", nameHuman: "-PEPS / --pixelit_endpoints"),
                        new("TP",   "path",    true,  section: "PixelIt", nameHuman: "-TP / --templates_path"),
                        new("BRI",  "int[1..255]", false, section: "PixelIt", nameHuman: "-BRI / --effect_brightness"),
                        new("IDE",  "string",  false, isMulti: true, section: "PixelIt", nameHuman: "-IDE / --idle_effects"),
                        new("G",    "string",  false, isMulti: true, section: "PixelIt", nameHuman: "-G / --game_won_effects"),
                        new("DEB",  "bool",    false, section: "Service", nameHuman: "-DEB / --debug",
                            valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" })
                    }
                )
            ));
        }

        // Inject logger into all freshly-created apps
        foreach (var app in AppsDownloadable) app.Logger = _logger;
        AppsAll.AddRange(AppsDownloadable);
        SaveApps();

        _logger.LogInformation("Created {N} default apps for {OS}-{Arch}",
            AppsDownloadable.Count, PlatformHelper.OSName, PlatformHelper.Architecture);
    }

    private void _CreateDefaultProfiles()
    {
        var apps = new Dictionary<string, ProfileState>();

        if (AppsDownloadable.Any(a => a.Name == "darts-caller"))
            apps["darts-caller"] = new ProfileState(taggedForStart: true);

        if (AppsDownloadable.Any(a => a.Name == "darts-wled"))
            apps["darts-wled"] = new ProfileState();

        if (AppsDownloadable.Any(a => a.Name == "darts-pixelit"))
            apps["darts-pixelit"] = new ProfileState();

        Profiles.Add(new Profile("default", apps));
        _BindAppsToProfiles();
        SaveApps();
    }
}
