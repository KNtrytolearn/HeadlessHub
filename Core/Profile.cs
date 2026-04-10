using Newtonsoft.Json;

namespace HeadlessHub.Core;

/// <summary>
/// A named collection of apps with per-app runtime constraints.
/// </summary>
public class Profile
{
    [JsonProperty("Name")]
    public string Name { get; }

    [JsonProperty("Apps")]
    public Dictionary<string, ProfileState> Apps { get; }

    [JsonProperty("IsTaggedForStart")]
    public bool IsTaggedForStart { get; set; }

    public Profile(string name, Dictionary<string, ProfileState> apps, bool isTaggedForStart = false)
    {
        Name = name;
        Apps = apps;
        IsTaggedForStart = isTaggedForStart;
    }
}

/// <summary>
/// The runtime constraints for one app inside a Profile.
/// Not serialized: App is injected by ProfileManager after deserialization.
/// </summary>
public class ProfileState
{
    [JsonProperty("IsRequired")]
    public bool IsRequired { get; }

    private bool _taggedForStart;

    [JsonProperty("TaggedForStart")]
    public bool TaggedForStart
    {
        get => _taggedForStart;
        set { if (!IsRequired) _taggedForStart = value; }
    }

    [JsonProperty("RuntimeArguments")]
    public Dictionary<string, string>? RuntimeArguments { get; private set; }

    [JsonIgnore]
    public AppBase? App { get; private set; }

    public ProfileState(bool isRequired = false, bool taggedForStart = false,
        Dictionary<string, string>? runtimeArguments = null)
    {
        IsRequired = isRequired;
        _taggedForStart = IsRequired || taggedForStart;
        RuntimeArguments = runtimeArguments;
    }

    /// <summary>Called by ProfileManager after JSON deserialization.</summary>
    public void Bind(AppBase app) => App = app;
}
