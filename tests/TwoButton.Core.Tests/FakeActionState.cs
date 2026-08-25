using TwoButton.Core.Model;

namespace TwoButton.Core.Tests;

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

    public bool IsUnlocked(uint actionId) => !_locked.Contains(actionId);

    public float CooldownRemaining(uint actionId) =>
        _cooldowns.TryGetValue(actionId, out var cd) ? cd : 0f;

    public int ChargesAvailable(uint actionId) =>
        _charges.TryGetValue(actionId, out var charges) ? charges : 1;

    public int MaxCharges(uint actionId) =>
        _maxCharges.TryGetValue(actionId, out var max) ? max : 1;

    public bool CanUse(uint actionId) => !_unusable.Contains(actionId);
}
