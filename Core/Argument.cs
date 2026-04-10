using Newtonsoft.Json;
using System.Globalization;

namespace HeadlessHub.Core;

/// <summary>
/// Describes a single CLI argument for an application's configuration.
/// </summary>
public class Argument
{
    private string? _value;

    [JsonProperty("Name")]
    public string Name { get; }

    [JsonProperty("Required")]
    public bool Required { get; set; }

    [JsonProperty("Type")]
    public string Type { get; }

    [JsonProperty("Section")]
    public string? Section { get; }

    [JsonProperty("Description")]
    public string? Description { get; }

    [JsonProperty("NameHuman")]
    public string? NameHuman { get; }

    /// <summary>Only required when the named argument has a specific value.</summary>
    [JsonProperty("RequiredOnArgument")]
    public string? RequiredOnArgument { get; set; }

    [JsonProperty("EmptyAllowedOnRequired")]
    public bool EmptyAllowedOnRequired { get; }

    /// <summary>If true, Value is NOT persisted to JSON (runtime-only).</summary>
    [JsonProperty("IsRuntimeArgument")]
    public bool IsRuntimeArgument { get; }

    [JsonProperty("IsMulti")]
    public bool IsMulti { get; }

    /// <summary>The current value. Skipped in JSON when IsRuntimeArgument is true.</summary>
    [JsonProperty("Value")]
    public string? Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                if (_value != null && value != null) _isDirty = true;
                _value = value;
            }
        }
    }

    [JsonProperty("ValueMapping")]
    public Dictionary<string, string>? ValueMapping { get; set; }

    /// <summary>Not serialized. Tracks whether the user changed this value since last save.</summary>
    [JsonIgnore]
    public bool IsDirty => _isDirty;
    private bool _isDirty;

    [JsonIgnore]
    public string RangeMin { get; private set; } = string.Empty;

    [JsonIgnore]
    public string RangeMax { get; private set; } = string.Empty;

    // ── Type constants ──────────────────────────────────────────────────────────
    public const string TypeString  = "string";
    public const string TypeFloat   = "float";
    public const string TypeInt     = "int";
    public const string TypeBool    = "bool";
    public const string TypeFile    = "file";
    public const string TypePath    = "path";
    public const string TypePassword= "password";
    public const string TypeSelection= "selection";
    public const string RangeDelim  = "..";

    public Argument(string name, string type, bool required,
        string? section = null, string? description = null, string? nameHuman = null,
        string? requiredOnArgument = null, bool emptyAllowedOnRequired = false,
        bool isRuntimeArgument = false, bool isMulti = false, string? value = null,
        Dictionary<string, string>? valueMapping = null)
    {
        Name = name;
        Required = required;
        Type = type.ToLowerInvariant();
        Section = section;
        Description = description;
        NameHuman = string.IsNullOrEmpty(nameHuman) ? name : nameHuman;
        RequiredOnArgument = requiredOnArgument;
        EmptyAllowedOnRequired = emptyAllowedOnRequired;
        IsRuntimeArgument = isRuntimeArgument;
        IsMulti = isMulti;
        _value = value;
        ValueMapping = valueMapping;
        ValidateType();          // throws ArgumentException on bad type string
    }

    /// <summary>Applies the user-friendly value via ValueMapping table, if present.</summary>
    public string? MappedValue() =>
        ValueMapping != null && Value != null
            ? ValueMapping.TryGetValue(Value, out var mapped) ? mapped : Value
            : Value;

    /// <summary>Suppress Newtonsoft from serializing runtime-only values.</summary>
    public bool ShouldSerializeValue() => !IsRuntimeArgument;

    /// <summary>Checks that the Type string is well-formed. Throws on invalid.</summary>
    public void ValidateType()
    {
        if (Type.StartsWith(TypeString) || Type.StartsWith(TypeFloat) || Type.StartsWith(TypeInt))
        {
            if (Type.Length > TypeString.Length && Type.StartsWith(TypeString))
                ParseRange(TypeString);
            else if (Type.Length > TypeFloat.Length && Type.StartsWith(TypeFloat))
                ParseRange(TypeFloat);
            else if (Type.Length > TypeInt.Length && Type.StartsWith(TypeInt))
                ParseRange(TypeInt);
        }
        else if (Type != TypeBool && Type != TypeFile && Type != TypePath &&
                 Type != TypePassword && !Type.StartsWith(TypeSelection))
        {
            throw new ArgumentException($"Argument-Type '{Type}' is invalid: {NameHuman}");
        }
    }

    /// <summary>Validates the user's current value. Throws ArgumentException on failure.</summary>
    public void Validate()
    {
        if (Required && string.IsNullOrEmpty(Value) && !EmptyAllowedOnRequired)
            throw new ArgumentException($"is required: {NameHuman}");

        switch (Type)
        {
            case var t when t.StartsWith(TypeFloat): ValidateFloat(); break;
            case var t when t.StartsWith(TypeInt):   ValidateInt();   break;
            case TypeBool:                           ValidateBool();  break;
            case TypeFile:
            case TypePath:                            ValidatePath();  break;
        }
    }

    /// <summary>Call after persisting, so IsDirty resets.</summary>
    public void MarkClean() => _isDirty = false;

    // ── Private helpers ─────────────────────────────────────────────────────────

    private void ParseRange(string type)
    {
        var range = Type[type.Length..];
        var parts = range.Split(RangeDelim);
        if (parts.Length == 2)
        {
            RangeMin = parts[0].TrimStart('[');
            RangeMax = parts[1].TrimEnd(']');
        }
    }

    private void ValidateFloat()
    {
        if (string.IsNullOrEmpty(Value)) return;
        if (!float.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            throw new ArgumentException($"Invalid float: {Value}");

        if (!string.IsNullOrEmpty(RangeMin) && !string.IsNullOrEmpty(RangeMax))
        {
            var min = float.Parse(RangeMin, CultureInfo.InvariantCulture);
            var max = float.Parse(RangeMax, CultureInfo.InvariantCulture);
            if (val < min || val > max)
                throw new ArgumentException($"Out of range ({RangeMin} to {RangeMax}): {NameHuman}");
        }
    }

    private void ValidateInt()
    {
        if (string.IsNullOrEmpty(Value)) return;
        if (!int.TryParse(Value, out var val))
            throw new ArgumentException($"Invalid integer: {Value}");

        if (!string.IsNullOrEmpty(RangeMin) && !string.IsNullOrEmpty(RangeMax))
        {
            var min = int.Parse(RangeMin);
            var max = int.Parse(RangeMax);
            if (val < min || val > max)
                throw new ArgumentException($"Out of range ({RangeMin} to {RangeMax}): {NameHuman}");
        }
    }

    private void ValidateBool()
    {
        if (string.IsNullOrEmpty(Value)) return;
        var v = Value.ToLowerInvariant();
        if (v != "true" && v != "false" && v != "1" && v != "0" && v != "yes" && v != "no")
            throw new ArgumentException($"Invalid boolean: {Value}");
    }

    private void ValidatePath()
    {
        if (!string.IsNullOrEmpty(Value))
        {
            try { _ = Path.GetFullPath(Value); }
            catch { throw new ArgumentException($"Invalid path: {Value}"); }
        }
    }
}
