using Newtonsoft.Json;

namespace HeadlessHub.Core;

/// <summary>
/// Describes the CLI argument structure of an AppBase. Can generate a
/// formatted argument string from the current Argument values.
/// </summary>
public class Configuration
{
    [JsonProperty("Prefix")]
    public string Prefix { get; }

    [JsonProperty("Delimiter")]
    public string Delimiter { get; }

    [JsonProperty("Arguments")]
    public List<Argument> Arguments { get; }

    /// <summary>
/// If true, only Arguments[1].Value is emitted (no flag prefix).
/// </summary>
    [JsonProperty("IsRaw")]
    public bool IsRaw { get; }

    /// <summary>Key prefix stored on thrown ArgumentException when validation fails.</summary>
    public const string ErrorKey = "HeadlessHub_ArgumentError_";

    public Configuration(string prefix, string delimiter, List<Argument> arguments, bool isRaw = false)
    {
        Prefix = prefix;
        Delimiter = delimiter;
        Arguments = arguments;
        IsRaw = isRaw;
    }

    /// <summary>
    /// Returns true if any Argument has IsDirty set.
    /// Clears all IsDirty flags in the process (call after persisting).
    /// </summary>
    public bool HasChanges()
    {
        var any = Arguments.Any(a => a.IsDirty);
        if (any) foreach (var a in Arguments) a.MarkClean();
        return any;
    }

    /// <summary>
    /// Overlays runtime arguments onto the matching Arguments by name,
    /// then builds the CLI string. Throws ArgumentException on validation failure.
    /// </summary>
    public string? BuildArgumentString(Dictionary<string, string>? runtimeArguments = null)
    {
        if (runtimeArguments != null)
        {
            foreach (var (k, v) in runtimeArguments)
            {
                var arg = Arguments.FirstOrDefault(a => a.Name == k);
                if (arg != null) arg.Value = v;
            }
        }

        if (IsRaw)
            return Arguments.Count >= 2 ? Arguments[1].Value : string.Empty;

        foreach (var a in Arguments)
            ApplyRequiredOnArgument(a);

        var active = Arguments
            .Where(a => a.Required || (!a.Required && !string.IsNullOrEmpty(a.Value)))
            .ToList();

        foreach (var a in active)
        {
            try { a.Validate(); }
            catch (ArgumentException ex)
            {
                var enriched = new ArgumentException($"{ErrorKey}{a.NameHuman}: {ex.Message}");
                enriched.Data["argument"] = a;
                throw enriched;
            }
        }

        var sb = new System.Text.StringBuilder();
        foreach (var a in active)
        {
            var mv = a.MappedValue();

            if (string.IsNullOrEmpty(mv))
                sb.Append($" {a.Name}");
            else if (!a.IsMulti)
                sb.Append($" {Prefix}{a.Name}{Delimiter}\"{mv}\"");
            else
            {
                var parts = mv.Split(' ');
                foreach (var p in parts)
                    sb.Append($" {Prefix}{a.Name}{Delimiter}\"{p}\"");
            }
        }

        return sb.ToString();
    }

    private void ApplyRequiredOnArgument(Argument a)
    {
        if (string.IsNullOrEmpty(a.RequiredOnArgument)) return;
        var parts = a.RequiredOnArgument.Split('=', 2);
        if (parts.Length != 2) return;

        var dep = Arguments.FirstOrDefault(x => x.Name == parts[0]);
        if (dep != null) a.Required = dep.Value == parts[1];
    }
}
