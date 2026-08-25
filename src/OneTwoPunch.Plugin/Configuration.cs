using Dalamud.Configuration;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Plugin;

/// <summary>
/// Settings are plain fields rather than properties so the ImGui widgets can bind to them
/// directly with <c>ref</c>.
/// </summary>
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Master switch. When off the hook passes every action straight through.</summary>
    public bool Enabled = true;

    // ---- Rotation behaviour (mirrors RotationSettings) --------------------

    public WeaveStyle WeaveStyle = WeaveStyle.Single;

    public bool UseOpener = true;

    public bool AoeFallsBackToSingleTarget = true;

    public bool SuggestPositionalRescue = true;

    public bool HoldBurstDuringDowntime = true;

    public float SuggestionHoldSeconds = 0.15f;

    public float WeaveSafetyMargin = 0.10f;

    /// <summary>
    /// Positional detection depends on reading the target's facing, which is the least
    /// certain thing the plugin does. Turning it off only costs the True North hint.
    /// </summary>
    public bool DetectPositionals = true;

    // ---- Potion ----------------------------------------------------------

    public bool PotionEnabled;

    /// <summary>Item id of the chosen potion. Zero means none picked yet.</summary>
    public uint PotionItemId;

    /// <summary>Use the high quality version when the player is carrying one.</summary>
    public bool PotionPreferHq = true;

    public bool PotionInOpener = true;

    public bool PotionOnBurst = true;

    // ---- Heads-up display ------------------------------------------------

    public bool ShowPreview = true;

    /// <summary>Also show the GCD that comes after the current suggestion.</summary>
    public bool ShowNextGcd = true;

    /// <summary>Show the one-line reason the engine picked this action.</summary>
    public bool ShowReason = true;

    /// <summary>Show a banner when it is the moment to pop a potion.</summary>
    public bool ShowPotionPrompt = true;

    /// <summary>
    /// Show a large banner telling you which side of the target to stand on for the coming
    /// global cooldown. On by default: positionals are the part of melee that costs the most
    /// damage when hands are the bottleneck, and a warning a global early is the difference
    /// between making one and missing it.
    /// </summary>
    public bool ShowPositionalBanner = true;

    /// <summary>Icon scale for the preview window. Large by default; this is the point.</summary>
    public float PreviewScale = 1.6f;

    public bool LockPreviewWindow;

    public bool ShowPreviewOnlyInCombat = true;

    // ---- Diagnostics -----------------------------------------------------

    public bool VerboseLogging;

    public RotationSettings ToRotationSettings() => new()
    {
        WeaveStyle = WeaveStyle,
        UseOpener = UseOpener,
        AoeFallsBackToSingleTarget = AoeFallsBackToSingleTarget,
        SuggestPositionalRescue = SuggestPositionalRescue,
        HoldBurstDuringDowntime = HoldBurstDuringDowntime,
        SuggestionHoldSeconds = SuggestionHoldSeconds,
        WeaveSafetyMargin = WeaveSafetyMargin,
        PotionEnabled = PotionEnabled && PotionItemId != 0,
        PotionInOpener = PotionInOpener,
        PotionOnBurst = PotionOnBurst,
    };

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
