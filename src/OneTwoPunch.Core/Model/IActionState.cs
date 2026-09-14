namespace OneTwoPunch.Core.Model;

/// <summary>
/// Live per-action state. Implemented by the plugin over <c>ActionManager</c>, and by a
/// fake in the tests. Queries must be cheap: the engine calls these many times per frame.
/// </summary>
public interface IActionState
{
    /// <summary>True if the action is unlocked for the player's current job and level.</summary>
    bool IsUnlocked(uint actionId);

    /// <summary>Seconds until the action comes off cooldown. Zero when ready.</summary>
    float CooldownRemaining(uint actionId);

    /// <summary>Charges currently available. Actions without charges report 0 or 1.</summary>
    int ChargesAvailable(uint actionId);

    int MaxCharges(uint actionId);

    /// <summary>
    /// True when the game itself would accept the action right now: in range, facing,
    /// resources available, target valid. Mirrors <c>GetActionStatus() == 0</c>.
    /// </summary>
    /// <param name="ignoreRecast">
    /// When true, every check is made except whether the recast has finished. This is how a
    /// global cooldown is chosen: the engine is picking what to press <em>next</em>, while
    /// the current global is still rolling, so requiring the recast to be over would mean no
    /// global could ever be chosen - the caller checks separately that it will be ready in
    /// time.
    /// </param>
    bool CanUse(uint actionId, bool ignoreRecast = false);

    /// <summary>
    /// The id the game currently hands back for this action - its upgrade, or the form a
    /// mechanic has put on it right now. Mirrors <c>GetAdjustedActionId</c>.
    /// <para>
    /// This is a read of live state, not a table of upgrades. Ninja's <c>Ninjutsu</c> is the
    /// reason it exists: asking what that id resolves to names the spell the charged mudras
    /// would cast, which is the only way to tell a half-finished three-mudra sequence from a
    /// finished two-mudra one. See <c>NinjaRotation.BuildMudraButton</c>.
    /// </para>
    /// <para>
    /// Implementations return <paramref name="actionId"/> unchanged when they cannot ask.
    /// </para>
    /// </summary>
    uint CurrentFormOf(uint actionId) => actionId;

    /// <summary>
    /// The game's own reason for refusing an action, or 0 when it would accept it. Mirrors
    /// <c>GetActionStatus</c>'s return value.
    /// <para>
    /// For the recorder only. "Refused" is a single bit in <see cref="CanUse"/> and that bit
    /// cost three rounds of guessing at a silent rule, because the reasons are many and they
    /// do not look alike: wrong target, resource missing, a prerequisite buff absent, another
    /// cooldown holding the slot.
    /// </para>
    /// </summary>
    int RefusalCode(uint actionId) => 0;
}
