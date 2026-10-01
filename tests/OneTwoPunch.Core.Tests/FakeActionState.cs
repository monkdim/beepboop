using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Stand-in for the game's ActionManager. Everything is unlocked, off cooldown and usable
/// unless a test says otherwise, so each test only states the thing it is actually about.
/// </summary>
public sealed class FakeActionState : IActionState
{
    private readonly Dictionary<uint, float> _cooldowns = [];
    private readonly Dictionary<uint, int> _charges = [];
    private readonly Dictionary<uint, int> _maxCharges = [];
    private readonly HashSet<uint> _locked = [];
    private readonly HashSet<uint> _unusable = [];
    private readonly HashSet<uint> _rolling = [];
    private readonly Dictionary<uint, int> _reasons = [];
    private readonly Dictionary<uint, uint> _forms = [];

    public FakeActionState OnCooldown(uint actionId, float seconds)
    {
        _cooldowns[actionId] = seconds;
        _charges[actionId] = 0;
        return this;
    }

    public FakeActionState WithCharges(uint actionId, int available, int max)
    {
        _charges[actionId] = available;
        _maxCharges[actionId] = max;
        _cooldowns[actionId] = available > 0 ? 0f : 30f;
        return this;
    }

    public FakeActionState Locked(uint actionId)
    {
        _locked.Add(actionId);
        return this;
    }

    public FakeActionState Unusable(uint actionId)
    {
        _unusable.Add(actionId);
        return this;
    }

    /// <summary>Takes back an <see cref="Unusable"/>, for a test about something changing.</summary>
    public FakeActionState Usable(uint actionId)
    {
        _unusable.Remove(actionId);
        return this;
    }

    /// <summary>
    /// Says what the game currently resolves an action to. Ninja's mudra button reads
    /// Ninjutsu this way, so a test states the charged spell rather than a press count.
    /// </summary>
    public FakeActionState Resolving(uint actionId, uint toActionId)
    {
        _forms[actionId] = toActionId;
        return this;
    }

    public uint CurrentFormOf(uint actionId) =>
        _forms.TryGetValue(actionId, out var form) ? form : actionId;

    public bool IsUnlocked(uint actionId) => !_locked.Contains(actionId);

    public float CooldownRemaining(uint actionId) =>
        _cooldowns.TryGetValue(actionId, out var cd) ? cd : 0f;

    public int ChargesAvailable(uint actionId) =>
        _charges.TryGetValue(actionId, out var charges) ? charges : 1;

    public int MaxCharges(uint actionId) =>
        _maxCharges.TryGetValue(actionId, out var max) ? max : 1;

    /// <summary>
    /// Refused this instant but acceptable by the next global - which is what a global that is
    /// simply still rolling looks like, and is the reading the probe has to tell apart from a
    /// rule that can never fire at all.
    /// </summary>
    public FakeActionState StillRolling(uint actionId)
    {
        _rolling.Add(actionId);
        return this;
    }

    public bool CanUse(uint actionId, bool ignoreRecast = false)
    {
        if (_unusable.Contains(actionId))
            return false;

        return ignoreRecast || !_rolling.Contains(actionId);
    }

    /// <summary>
    /// Refused for a stated reason. 572 is the game's "cannot use yet" - a prerequisite that
    /// is simply absent - which several rules now tell apart from a timer that is running.
    /// </summary>
    public FakeActionState Refused(uint actionId, int reason)
    {
        _unusable.Add(actionId);
        _reasons[actionId] = reason;
        return this;
    }

    /// <summary>A stand-in reason, so the probe's shape is exercised without inventing codes.</summary>
    public int RefusalCode(uint actionId) =>
        _reasons.TryGetValue(actionId, out var why) ? why
        : _unusable.Contains(actionId) ? 566
        : _rolling.Contains(actionId) ? 582
        : 0;

    /// <summary>
    /// The reason for the question a global is judged by. A merely rolling recast has none,
    /// because setting the recast aside is exactly what this question does.
    /// </summary>
    public int RefusalCode(uint actionId, bool ignoreRecast) =>
        ignoreRecast
            ? (_unusable.Contains(actionId) ? RefusalCode(actionId) : 0)
            : RefusalCode(actionId);
}
