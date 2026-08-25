using TwoButton.Core.Model;

namespace TwoButton.Core.Engine;

/// <summary>
/// An optional third or fourth button for a mechanic the two main buttons cannot express.
/// <para>
/// Extra buttons are opt-in per job and are for mechanics with their own sequence - mudras,
/// dance steps - rather than for spilling rotation over into more keys. If a job needs one
/// to be played at all, that is worth saying out loud in <see cref="Purpose"/> so nobody
/// binds it expecting a damage gain.
/// </para>
/// </summary>
public sealed class ExtraButton(ActionRef host, string name, string purpose, bool respectWeaveWindow = false)
{
    /// <summary>The action the player puts on their hotbar for this button.</summary>
    public ActionRef Host { get; } = host;

    /// <summary>Short label, e.g. "Mudra".</summary>
    public string Name { get; } = name;

    /// <summary>One line explaining what it is for, shown in the setup panel.</summary>
    public string Purpose { get; } = purpose;

    /// <summary>
    /// Whether the global-cooldown and weave rules apply. Most extra buttons drive their own
    /// sequence where those rules do not make sense - mudras are pressed back to back and do
    /// not roll the GCD - so the default is simply "first matching rule wins".
    /// </summary>
    public bool RespectWeaveWindow { get; } = respectWeaveWindow;

    public RotationPlan Plan { get; } = new();
}
