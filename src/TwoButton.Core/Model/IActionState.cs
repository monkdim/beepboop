namespace TwoButton.Core.Model;

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
    bool CanUse(uint actionId);
}
