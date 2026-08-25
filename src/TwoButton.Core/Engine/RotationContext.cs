using TwoButton.Core.Model;

namespace TwoButton.Core.Engine;

/// <summary>
/// The view of the world a rotation rule gets. Everything a job file needs should be
/// reachable from here as a short, readable expression, so a priority list looks like the
/// rotation guide it was copied from.
/// </summary>
public sealed class RotationContext
{
    private readonly CombatSnapshot _snapshot;
    private readonly IActionState _actions;

    internal RotationContext(
        CombatSnapshot snapshot,
        IActionState actions,
        RotationSettings settings,
        RotationMode mode,
        int weavesUsedThisWindow)
    {
        _snapshot = snapshot;
        _actions = actions;
        Settings = settings;
        Mode = mode;
        WeavesUsedThisWindow = weavesUsedThisWindow;
    }

    public RotationSettings Settings { get; }

    public RotationMode Mode { get; }

    /// <summary>Off-globals already woven into the current GCD window.</summary>
    public int WeavesUsedThisWindow { get; }

    public CombatSnapshot Snapshot => _snapshot;

    // ---- Basics ----------------------------------------------------------

    public byte Level => _snapshot.Level;

    public bool InCombat => _snapshot.InCombat;

    public float CombatDuration => _snapshot.CombatDuration;

    public bool Moving => _snapshot.IsMoving;

    /// <summary>Seconds of continuous movement so far. Zero when standing still.</summary>
    public float MovingFor => _snapshot.MovingFor;

    public bool HasTarget => _snapshot.HasTarget && _snapshot.TargetIsHostile;

    public bool InRange => _snapshot.TargetInRange;

    public float TargetHp => _snapshot.TargetHpFraction;

    public RelativePosition Position => _snapshot.Position;

    /// <summary>
    /// Enemy count to plan around. The single-target button always plans for one, so a
    /// stray add never rewrites the single-target rotation mid-pull.
    /// </summary>
    public int Enemies => Mode == RotationMode.Aoe ? Math.Max(1, _snapshot.EnemiesInAoeRange) : 1;

    /// <summary>True when burst should be held: boss untargetable, or nothing to hit.</summary>
    public bool Downtime =>
        Settings.HoldBurstDuringDowntime && (_snapshot.InDowntime || !HasTarget);

    // ---- Look-ahead ------------------------------------------------------

    /// <summary>
    /// The GCD that will be suggested next. Resolved before any off-global rule is
    /// evaluated, so a weave can be conditioned on the hit it is meant to buff -
    /// "Reassemble, but only in front of a tool" rather than "Reassemble, roughly now".
    /// Null while the GCD list itself is being evaluated.
    /// </summary>
    public ActionRef? NextGcd { get; internal set; }

    /// <summary>Positional wanted by <see cref="NextGcd"/>.</summary>
    public PositionalHint NextPositional { get; internal set; }

    public bool NextGcdIs(ActionRef action) => NextGcd is not null && NextGcd.Id == action.Id;

    /// <summary>True when the upcoming GCD is any of the given actions.</summary>
    public bool NextGcdIsAny(params ActionRef[] actions)
    {
        if (NextGcd is null)
            return false;

        foreach (var action in actions)
        {
            if (NextGcd.Id == action.Id)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when the next GCD is close enough that a buff spent now will still be up for
    /// it. Keeps single-charge buffs from being thrown two globals early.
    /// </summary>
    public bool GcdImminent => GcdRemaining <= Settings.AssumedAnimationLock * 2f;

    // ---- Gauges ----------------------------------------------------------

    public ref DragoonGauge Drg => ref _snapshot.Gauges.Dragoon;

    public ref MachinistGauge Mch => ref _snapshot.Gauges.Machinist;

    // ---- Actions ---------------------------------------------------------

    /// <summary>True if the player has learned the action at their current level.</summary>
    public bool Has(ActionRef action) =>
        Level >= action.Level && _actions.IsUnlocked(action.Id);

    /// <summary>Seconds until the action is off cooldown.</summary>
    public float Cd(ActionRef action) => _actions.CooldownRemaining(action.Id);

    public int Charges(ActionRef action) => _actions.ChargesAvailable(action.Id);

    public int MaxCharges(ActionRef action) => _actions.MaxCharges(action.Id);

    /// <summary>
    /// Usable right now: learned, off cooldown (or holding a charge), and accepted by the
    /// game. This is the gate every rule passes through, so a rule can never suggest
    /// something that would produce an error noise.
    /// </summary>
    public bool Ready(ActionRef action)
    {
        if (!Has(action))
            return false;

        var hasCharge = MaxCharges(action) > 1
            ? Charges(action) > 0
            : Cd(action) <= Settings.GcdReadyThreshold;

        return hasCharge && _actions.CanUse(action.Id);
    }

    /// <summary>True if the action comes up within <paramref name="seconds"/>.</summary>
    public bool ReadyIn(ActionRef action, float seconds) =>
        Has(action) && Cd(action) <= seconds;

    /// <summary>
    /// True if the cooldown will still be rolling after the next GCD. Used to decide
    /// whether a charge can be held without losing a use.
    /// </summary>
    public bool WillCapBefore(ActionRef action, float seconds) =>
        Has(action) && Cd(action) <= seconds;

    // ---- Combo -----------------------------------------------------------

    /// <summary>True if the given action is the live combo step.</summary>
    public bool ComboIs(ActionRef action) =>
        _snapshot.ComboTimeRemaining > 0f && _snapshot.LastComboAction == action.Id;

    public bool ComboBroken => _snapshot.ComboTimeRemaining <= 0f;

    public float ComboTimeLeft => _snapshot.ComboTimeRemaining;

    // ---- Statuses --------------------------------------------------------

    public bool Buff(StatusRef status) => BuffTime(status) > 0f;

    public float BuffTime(StatusRef status) => Find(_snapshot.SelfStatuses, status.Id).remaining;

    public int BuffStacks(StatusRef status) => Find(_snapshot.SelfStatuses, status.Id).stacks;

    public bool Debuff(StatusRef status) => DebuffTime(status) > 0f;

    public float DebuffTime(StatusRef status) => Find(_snapshot.TargetStatuses, status.Id).remaining;

    /// <summary>
    /// True when a damage-over-time needs refreshing: missing, or inside the reapply window.
    /// </summary>
    public bool DotExpiring(StatusRef status, float within = 3f) => DebuffTime(status) < within;

    private static (float remaining, int stacks) Find(IReadOnlyList<StatusEntry> list, uint id)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id)
                return (list[i].Remaining, list[i].Stacks);
        }

        return (0f, 0);
    }

    // ---- Weaving ---------------------------------------------------------

    public float GcdRemaining => _snapshot.GcdRemaining;

    public float GcdTotal => _snapshot.GcdTotal;

    /// <summary>True when a GCD can be pressed this instant.</summary>
    public bool GcdReady => _snapshot.GcdRemaining <= Settings.GcdReadyThreshold;

    /// <summary>
    /// True when an off-global fits in the remaining GCD gap without clipping.
    /// <para>
    /// The gap has to swallow whatever animation lock is still running, plus the lock the
    /// new ability will impose, plus a safety margin - and we have to be under the weave
    /// budget the player configured.
    /// </para>
    /// </summary>
    public bool CanWeave
    {
        get
        {
            if (Settings.MaxWeavesPerGcd <= 0)
                return false;

            if (WeavesUsedThisWindow >= Settings.MaxWeavesPerGcd)
                return false;

            var needed = _snapshot.AnimationLock
                + Settings.AssumedAnimationLock
                + Settings.WeaveSafetyMargin;

            return _snapshot.GcdRemaining >= needed;
        }
    }

    /// <summary>
    /// True when this is the last weave slot before the GCD comes up. Useful for abilities
    /// that want to land as late as possible in the window.
    /// </summary>
    public bool LastWeaveSlot
    {
        get
        {
            if (!CanWeave)
                return false;

            var oneMore = _snapshot.AnimationLock
                + (Settings.AssumedAnimationLock * 2f)
                + Settings.WeaveSafetyMargin;

            return _snapshot.GcdRemaining < oneMore
                || WeavesUsedThisWindow + 1 >= Settings.MaxWeavesPerGcd;
        }
    }
}
