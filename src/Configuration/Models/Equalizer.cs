using LunaPlayer.Equalizer;

namespace LunaPlayer.Configuration.Models;

/// <summary>One band of a preset the player ships, as the user has changed it.</summary>
///
/// <remarks>
/// Only what was changed is written. A band the user left alone has all three of these null and follows
/// whatever the player ships, so improving a preset in a later release still reaches everyone who only
/// moved one band of it.
/// </remarks>
internal sealed class EqualizerBandOverride
{
    /// <summary>Which of the equalizer's slots this changes.</summary>
    public int Slot { get; set; }

    public double? Frequency { get; set; }
    public double? Q { get; set; }
    public double? Gain { get; set; }

    internal EqualizerBandOverride Copy()
        => new() { Slot = Slot, Frequency = Frequency, Q = Q, Gain = Gain };

    internal bool IsEmpty => Frequency is null && Q is null && Gain is null;
}

internal sealed class EqualizerSettings
{
    public bool Enabled { get; set; }

    /// <summary>The preset's stable name, not the one shown in the menu.</summary>
    /// <remarks>
    /// Kept even while the equalizer is switched off, so that turning it back on returns to the curve the
    /// user last chose rather than to whatever comes first in the list.
    /// </remarks>
    public string Preset { get; set; } = Presets.DefaultId;

    /// <summary>What the user has changed about the presets the player ships, keyed by preset name.
    /// </summary>
    /// <remarks>
    /// Presets the user made themselves are not here. Those are whole presets and live in their own file;
    /// these are differences, and a difference is meaningless apart from the thing it differs from.
    /// A preset with no entry here is exactly as it ships, which is also what resetting one to its
    /// defaults does: the entry is removed rather than filled with the defaults.
    /// </remarks>
    public Dictionary<string, List<EqualizerBandOverride>> Overrides { get; set; } = [];

    internal EqualizerSettings Copy()
    {
        var copy = new EqualizerSettings { Enabled = Enabled, Preset = Preset };
        foreach (var (preset, bands) in Overrides)
            copy.Overrides[preset] = [.. bands.Select(band => band.Copy())];
        return copy;
    }

    internal void Apply(EqualizerSettings source)
    {
        Enabled = source.Enabled;
        Preset = source.Preset;
        Overrides.Clear();
        foreach (var (preset, bands) in source.Overrides)
            Overrides[preset] = [.. bands.Select(band => band.Copy())];
    }
}
