namespace OneTwoPunch.Core.Model;

/// <summary>How aggressively the engine is allowed to pack off-globals into a GCD gap.</summary>
public enum WeaveStyle
{
    /// <summary>Never suggest an off-global while a GCD is available. Lowest APM.</summary>
    None = 0,

    /// <summary>At most one off-global per GCD window. The accessible default.</summary>
    Single = 1,

    /// <summary>Up to two off-globals per GCD window. Highest damage, highest APM.</summary>
    Double = 2,
}

/// <summary>
/// Tuning knobs. These are deliberately player-facing: the point of the plugin is that
/// somebody who cannot double-weave can turn double-weaving off and still play.
/// </summary>
public sealed class RotationSettings
{
    /// <summary>Weave budget per GCD window.</summary>
    public WeaveStyle WeaveStyle { get; set; } = WeaveStyle.Single;

    /// <summary>
    /// Animation lock assumed for an off-global we have not pressed yet. The real value is
    /// ~0.6s for most abilities; the engine budgets slightly more so a laggy frame does not
    /// turn a weave into a clipped GCD.
    /// </summary>
    public float AssumedAnimationLock { get; set; } = 0.65f;

    /// <summary>Extra head-room on top of the animation lock before a weave is offered.</summary>
    public float WeaveSafetyMargin { get; set; } = 0.10f;

    /// <summary>GCD remaining at or below which the GCD counts as "up".</summary>
    public float GcdReadyThreshold { get; set; } = 0.10f;

    /// <summary>
    /// Hold a suggestion on screen for at least this long before letting it change, so the
    /// icon does not flicker between two actions while somebody is reaching for the key.
    /// Set to zero to disable.
    /// </summary>
    public float SuggestionHoldSeconds { get; set; } = 0.15f;

    /// <summary>Run the job's scripted opener when combat starts.</summary>
    public bool UseOpener { get; set; } = true;

    /// <summary>
    /// How far into combat the opener may still start. Past this the fight is already under
    /// way - the plugin was loaded mid-pull, or somebody pulled early - and the priority
    /// list is the honest answer.
    /// </summary>
    public float OpenerGraceSeconds { get; set; } = 3f;

    /// <summary>
    /// When the AoE button is pressed and only one enemy is in range, fall back to the
    /// single-target rotation instead of suggesting a weak AoE GCD.
    /// </summary>
    public bool AoeFallsBackToSingleTarget { get; set; } = true;

    /// <summary>
    /// Suggest True North (or the job equivalent) when the next GCD wants a positional the
    /// player is not standing in. For players who cannot reliably reposition.
    /// </summary>
    public bool SuggestPositionalRescue { get; set; } = true;

    /// <summary>
    /// Do not suggest raid buffs or burst cooldowns while the boss is untargetable.
    /// </summary>
    public bool HoldBurstDuringDowntime { get; set; } = true;

    // ---- Potion ----------------------------------------------------------

    /// <summary>
    /// Prompt for a potion. A potion is an item, not an action, so the button itself cannot
    /// become one - see <see cref="Suggestion.PotionPrompt"/>. What the engine can do is
    /// tell you the exact weave window to pop it in.
    /// </summary>
    public bool PotionEnabled { get; set; }

    /// <summary>Prompt at the job opener's potion point.</summary>
    public bool PotionInOpener { get; set; } = true;

    /// <summary>Prompt whenever the job's burst window opens and the potion is off cooldown.</summary>
    public bool PotionOnBurst { get; set; } = true;

    /// <summary>Effective weave budget as a count.</summary>
    public int MaxWeavesPerGcd => (int)WeaveStyle;
}
