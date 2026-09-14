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

    private readonly Dictionary<uint, uint> _forms = [];

    private readonly record struct Entry(
        bool Unlocked,
        float Cooldown,
        int Charges,
        int MaxCharges,
        bool Usable,
        bool UsableIgnoringRecast);

    /// <summary>
    /// How to ask the game what an action currently resolves to. Set by the plugin to
    /// <c>ActionReplacer.CurrentFormOf</c>, which goes through the hook's <em>original</em>
    /// function rather than our detour - asking our own answer would be circular, and on the
    /// host action it would recurse.
    /// </summary>
    public Func<uint, uint>? FormResolver { get; set; }

    /// <summary>Drops the cache. Called once per resolve.</summary>
    public void BeginFrame(byte level, ulong targetId)
    {
        _level = level;
        _targetId = targetId;
        _cache.Clear();
        _forms.Clear();
    }

    public bool IsUnlocked(uint actionId) => Lookup(actionId).Unlocked;

    public float CooldownRemaining(uint actionId) => Lookup(actionId).Cooldown;

    public int ChargesAvailable(uint actionId) => Lookup(actionId).Charges;

    public int MaxCharges(uint actionId) => Lookup(actionId).MaxCharges;

    public bool CanUse(uint actionId, bool ignoreRecast = false) =>
        ignoreRecast ? Lookup(actionId).UsableIgnoringRecast : Lookup(actionId).Usable;

    /// <summary>
    /// Cached per frame like everything else here. Ninja's mudra button asks this several
    /// times while walking its priority list, and the answer cannot change inside one frame.
    /// </summary>
    public uint CurrentFormOf(uint actionId)
    {
        if (FormResolver is null || actionId == 0)
            return actionId;

        if (_forms.TryGetValue(actionId, out var cached))
            return cached;

        var form = FormResolver(actionId);
        if (form == 0)
            form = actionId;

        _forms[actionId] = form;
        return form;
    }

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

        // The authority on whether anything is actually on cooldown.
        //
        // GetRecastTimeElapsed reports zero for a group whose timer is not running, and zero
        // is also what it reports for an action used this instant - so the arithmetic above
        // cannot tell "never used" from "just used" and answers "a full recast to go" for
        // both. On a single-charge action that is mostly harmless, because the game refuses
        // it anyway and GetRecastTime tends to answer zero alongside. On a charged action it
        // is fatal: charges works out to elapsed/perCharge = 0, so the action reports no
        // charges and no rule can ever offer it.
        //
        // Ninja's mudras are the case that proved it, and the failure was a deadlock. Ten
        // holds two charges on a twenty second timer, and its group had never been started -
        // so it reported zero charges, so no rule suggested it, so it was never pressed, so
        // the group was never started. A recorded pull shows the button walking past Ten to
        // a Ninki spender three rules further down, in every weave window of eighty seconds.
        //
        // IsRecastTimerActive answers the question directly: not running means off cooldown,
        // with every charge in hand.
        if (!manager->IsRecastTimerActive(ActionType.Action, actionId))
        {
            remaining = 0f;
            charges = maxCharges;
        }

        // GetActionStatus reports 0 when the game would accept the action right now. This is
        // what keeps a suggestion from ever being something that just makes an error noise:
        // out of range, wrong target, not enough resource, not learned.
        // With the target. The parameter defaults to E0000000 - "no target" - and asking
        // whether a targeted action is usable on nothing always answers no, which silently
        // made every rule in every job unmatchable and left the button on its base attack.
        var status = manager->GetActionStatus(ActionType.Action, actionId, _targetId);

        // The same question with the recast and cast checks switched off. Choosing the next
        // global means asking "would this be legal apart from the things I am waiting out".
        //
        // checkCastingActive matters as much as checkRecastActive on a caster: mid-cast the
        // game answers no to every spell, because you cannot start one while another is
        // going off. A recorded Black Mage pull is full of "nothing to suggest" for exactly
        // the length of each cast - the button dropping back to Fire I while Fire IV was in
        // the air - because the look-ahead was asking whether the next spell could be cast
        // *now* rather than when the current one lands.
        var statusIgnoringRecast = manager->GetActionStatus(
            ActionType.Action, actionId, _targetId, checkRecastActive: false, checkCastingActive: false);
        var usable = status == 0;

        // 572 is "you have not learned this action"; treat anything that is purely a
        // targeting problem as still unlocked so range rules can see it.
        var unlocked = status != 572;

        return new Entry(unlocked, remaining, charges, maxCharges, usable, statusIgnoringRecast == 0);
    }
}
