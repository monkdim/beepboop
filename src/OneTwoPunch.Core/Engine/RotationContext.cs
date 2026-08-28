using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Engine;

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

    /// <summary>True while a cast is in flight. The game refuses every action during one.</summary>
    public bool Casting => _snapshot.IsCasting;

    /// <summary>Seconds of continuous movement so far. Zero when standing still.</summary>
    public float MovingFor => _snapshot.MovingFor;

    public bool HasTarget => _snapshot.HasTarget && _snapshot.TargetIsHostile;

    /// <summary>
    /// Whether the target is close enough to hit, once a brief loss of reach has been ridden
    /// out. See <see cref="CombatSnapshot.OutOfRangeFor"/> for why the raw answer flickers.
    /// <para>
    /// The dwell matters because every melee job's out-of-range rule sits above its filler
    /// combo: a single frame of "no" outranks the entire rotation beneath it, and the button
    /// jumps to the ranged global and back for one global.
    /// </para>
    /// </summary>
    public bool InRange => _snapshot.TargetInRange || _snapshot.OutOfRangeFor < RangeSwapDelay;

    /// <summary>
    /// How long the target has to stay out of reach before the rotation gives up on melee.
    /// <para>
    /// Under half a global: long enough to swallow a turn for a positional and the jitter
    /// around it, short enough that genuinely walking away still switches inside one global.
    /// </para>
    /// </summary>
    private const float RangeSwapDelay = 1f;

    /// <summary>The instant answer, undwelled. Nothing should want this except a diagnostic.</summary>
    public bool InRangeRightNow => _snapshot.TargetInRange;

    public float TargetHp => _snapshot.TargetHpFraction;

    public RelativePosition Position => _snapshot.Position;

    /// <summary>
    /// Enemy count to plan around. The single-target button always plans for one, so a
    /// stray add never rewrites the single-target rotation mid-pull.
    /// </summary>
    public int Enemies => Mode == RotationMode.SingleTarget
        ? 1
        : Math.Max(1, _snapshot.EnemiesInAoeRange);

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

    public ref PaladinGauge Pld => ref _snapshot.Gauges.Paladin;

    public ref WarriorGauge War => ref _snapshot.Gauges.Warrior;

    public ref DarkKnightGauge Drk => ref _snapshot.Gauges.DarkKnight;

    public ref GunbreakerGauge Gnb => ref _snapshot.Gauges.Gunbreaker;

    public ref MonkGauge Mnk => ref _snapshot.Gauges.Monk;

    public ref DragoonGauge Drg => ref _snapshot.Gauges.Dragoon;

    public ref BardGauge Brd => ref _snapshot.Gauges.Bard;

    public ref BlackMageGauge Blm => ref _snapshot.Gauges.BlackMage;

    public ref SummonerGauge Smn => ref _snapshot.Gauges.Summoner;

    public ref NinjaGauge Nin => ref _snapshot.Gauges.Ninja;

    public ref MachinistGauge Mch => ref _snapshot.Gauges.Machinist;

    public ref SamuraiGauge Sam => ref _snapshot.Gauges.Samurai;

    public ref RedMageGauge Rdm => ref _snapshot.Gauges.RedMage;

    public ref DancerGauge Dnc => ref _snapshot.Gauges.Dancer;

    public ref ReaperGauge Rpr => ref _snapshot.Gauges.Reaper;

    public ref ViperGauge Vpr => ref _snapshot.Gauges.Viper;

    public ref PictomancerGauge Pct => ref _snapshot.Gauges.Pictomancer;

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
    /// <summary>Current MP.</summary>
    public uint Mp => _snapshot.Mp;

    /// <param name="byNextGcd">
    /// Judge readiness as of the next global cooldown rather than this instant.
    /// <para>
    /// A global's cooldown <em>is</em> the shared global, so asking whether one is off
    /// cooldown right now is asking whether the global is up - and while it is rolling the
    /// answer is no for every global in the job. That left no rule able to match for most of
    /// every single global, so the button fell back to the base attack, and since the game
    /// queues a press made during the recast, that base attack is what got queued.
    /// </para>
    /// <para>
    /// So for globals the cooldown is allowed to be anything that will have elapsed by the
    /// time the global comes up. Everything else - target, range, resources, combo step,
    /// level - is still checked exactly as before, which is what keeps an action that is
    /// genuinely on a long cooldown of its own from being suggested.
    /// </para>
    /// </param>
    /// <summary>
    /// Readiness judged the way the action itself is used: a global as of the next global,
    /// an off-global as of right now.
    /// <para>
    /// This overload is what rule conditions call, and it defaulting to "right now" was
    /// wrong for globals in the same way the gate was. A rule reading
    /// <c>!c.Ready(Fire4)</c> means "there is no mana left for another Fire IV" - but with
    /// the instant meaning it read "the global is still rolling", which is true for most of
    /// every global, so Despair fired immediately after a Manafont had refilled the bar.
    /// </para>
    /// </summary>
    public bool Ready(ActionRef action) => Ready(action, action.Kind == ActionKind.Gcd);

    public bool Ready(ActionRef action, bool byNextGcd)
    {
        if (!Has(action))
            return false;

        var allowance = byNextGcd
            ? Math.Max(Settings.GcdReadyThreshold, _snapshot.GcdRemaining)
            : Settings.GcdReadyThreshold;

        var cd = Cd(action);
        var hasCharge = MaxCharges(action) > 1
            ? Charges(action) > 0 || cd <= allowance
            : cd <= allowance;

        return hasCharge && _actions.CanUse(action.Id, byNextGcd);
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

    /// <summary>
    /// Whether the status is on you at all. Deliberately a question about presence rather
    /// than about time left.
    /// <para>
    /// This used to be <c>BuffTime(status) &gt; 0</c>, which is the same thing right up
    /// until the game reports a status with no time on it. Black Mage's Thunderhead does
    /// exactly that - a recorded pull had it on the player for seventy seconds straight,
    /// every frame reporting zero seconds remaining - so both Thunder rules read it as
    /// absent and Thunder was never once suggested in a whole fight. A buff you are holding
    /// is a buff you are holding; ask <see cref="BuffTime"/> when the answer needs a clock.
    /// </para>
    /// </summary>
    public bool Buff(StatusRef status) => Has(_snapshot.SelfStatuses, status.Id);

    public float BuffTime(StatusRef status) => Find(_snapshot.SelfStatuses, status.Id).remaining;

    public int BuffStacks(StatusRef status) => Find(_snapshot.SelfStatuses, status.Id).stacks;

    /// <summary>Whether your own debuff is on the target. Presence, as with <see cref="Buff"/>.</summary>
    public bool Debuff(StatusRef status) => Has(_snapshot.TargetStatuses, status.Id);

    public float DebuffTime(StatusRef status) => Find(_snapshot.TargetStatuses, status.Id).remaining;

    /// <summary>
    /// True when a damage-over-time needs refreshing: missing, or inside the reapply window.
    /// </summary>
    public bool DotExpiring(StatusRef status, float within = 3f) => DebuffTime(status) < within;

    private static bool Has(IReadOnlyList<StatusEntry> list, uint id)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id)
                return true;
        }

        return false;
    }

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
