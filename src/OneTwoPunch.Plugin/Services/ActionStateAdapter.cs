using FFXIVClientStructs.FFXIV.Client.Game;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Plugin.Services;

/// <summary>
/// <see cref="IActionState"/> over the game's own ActionManager.
/// <para>
/// Results are cached for the duration of one resolve. The engine asks about the same
/// action several times while walking a priority list, and the game is asked many times a
/// second just to draw an icon.
/// </para>
/// </summary>
public sealed unsafe class ActionStateAdapter : IActionState
{
    private readonly Dictionary<uint, Entry> _cache = [];
    private byte _level = 1;
    private ulong _targetId = CombatSnapshot.NoTarget;

    private readonly record struct Entry(
        bool Unlocked,
        float Cooldown,
        int Charges,
        int MaxCharges,
        bool Usable,
        bool UsableIgnoringRecast);

    /// <summary>Drops the cache. Called once per resolve.</summary>
    public void BeginFrame(byte level, ulong targetId)
    {
        _level = level;
        _targetId = targetId;
        _cache.Clear();
    }

    public bool IsUnlocked(uint actionId) => Lookup(actionId).Unlocked;

    public float CooldownRemaining(uint actionId) => Lookup(actionId).Cooldown;

    public int ChargesAvailable(uint actionId) => Lookup(actionId).Charges;

    public int MaxCharges(uint actionId) => Lookup(actionId).MaxCharges;

    public bool CanUse(uint actionId, bool ignoreRecast = false) =>
        ignoreRecast ? Lookup(actionId).UsableIgnoringRecast : Lookup(actionId).Usable;

    private Entry Lookup(uint actionId)
    {
        if (_cache.TryGetValue(actionId, out var cached))
            return cached;

        var entry = Read(actionId);
        _cache[actionId] = entry;
        return entry;
    }

    private Entry Read(uint actionId)
    {
        var manager = ActionManager.Instance();
        if (manager is null || actionId == 0)
            return new Entry(false, float.MaxValue, 0, 1, false, false);

        var maxCharges = (int)ActionManager.GetMaxCharges(actionId, _level);
        if (maxCharges < 1)
            maxCharges = 1;

        var recast = manager->GetRecastTime(ActionType.Action, actionId);
        var elapsed = manager->GetRecastTimeElapsed(ActionType.Action, actionId);
        var remaining = Math.Max(0f, recast - elapsed);

        int charges;
        if (maxCharges > 1)
        {
            // Charges refill one per (total recast / max charges).
            var perCharge = recast / maxCharges;
            charges = perCharge > 0f ? (int)(elapsed / perCharge) : 0;
            charges = Math.Clamp(charges, 0, maxCharges);

            if (charges > 0)
                remaining = 0f;
        }
        else
        {
            charges = remaining <= 0f ? 1 : 0;
        }

        // GetActionStatus reports 0 when the game would accept the action right now. This is
        // what keeps a suggestion from ever being something that just makes an error noise:
        // out of range, wrong target, not enough resource, not learned.
        // With the target. The parameter defaults to E0000000 - "no target" - and asking
        // whether a targeted action is usable on nothing always answers no, which silently
        // made every rule in every job unmatchable and left the button on its base attack.
        var status = manager->GetActionStatus(ActionType.Action, actionId, _targetId);

        // The same question with the recast check switched off. Choosing the next global
        // means asking "would this be legal apart from the cooldown I am waiting out".
        var statusIgnoringRecast =
            manager->GetActionStatus(ActionType.Action, actionId, _targetId, checkRecastActive: false);
        var usable = status == 0;

        // 572 is "you have not learned this action"; treat anything that is purely a
        // targeting problem as still unlocked so range rules can see it.
        var unlocked = status != 572;

        return new Entry(unlocked, remaining, charges, maxCharges, usable, statusIgnoringRecast == 0);
    }
}
