namespace TwoButton.Core.Model;

/// <summary>
/// What the button should currently be. <see cref="Action"/> is what gets returned to the
/// game; the rest is context for the HUD.
/// </summary>
public sealed class Suggestion(
    ActionRef action,
    ActionRef? nextGcd = null,
    string? note = null,
    PositionalHint positional = PositionalHint.None)
{
    public ActionRef Action { get; } = action;

    /// <summary>
    /// The GCD that will come up next, even when <see cref="Action"/> is an off-global.
    /// Shown as a secondary icon so the player can see what is coming.
    /// </summary>
    public ActionRef? NextGcd { get; } = nextGcd;

    /// <summary>Short human explanation, e.g. "overcap protection" or "instant, you are moving".</summary>
    public string? Note { get; } = note;

    public PositionalHint Positional { get; } = positional;

    /// <summary>
    /// True when now is the moment to pop your potion.
    /// <para>
    /// A potion is an item, not an action, so it cannot be reached through the action hook
    /// this plugin is built on - and pressing it for you would make this an automation
    /// plugin. So the engine does the next best thing and tells you the exact weave window,
    /// having already confirmed the potion is off cooldown and that it fits without
    /// clipping. Bind it to one spare key; it is one press every few minutes.
    /// </para>
    /// </summary>
    public bool PotionPrompt { get; set; }

    public ActionKind Kind => Action.Kind;

    public override string ToString() =>
        Note is null ? Action.Name : $"{Action.Name} - {Note}";
}
