using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Engine;

/// <summary>
/// Stateful driver around a job's priority lists. One session lives for as long as the
/// player stays on a job.
/// <para>
/// <see cref="Resolve"/> has no side effects and is safe to call every frame - the game
/// asks what the button is many times a second just to draw the icon. State only changes
/// in <see cref="NotifyActionUsed"/>, which the plugin calls when an action actually goes
/// off.
/// </para>
/// </summary>
public sealed class RotationSession(IJobRotation job, RotationSettings settings)
{
    private HashSet<uint> _oGcdIds = BuildKindSet(job, ActionKind.OGcd);
    private HashSet<uint> _gcdIds = BuildKindSet(job, ActionKind.Gcd);

    private int _weavesThisWindow;
    private float _lastGcdRemaining;

    private int _openerStep;
    private bool _openerAborted;
    private bool _wasInCombat;

    private Suggestion? _held;
    private double _heldSince;

    public IJobRotation Job => job;

    public RotationSettings Settings => settings;

    /// <summary>Index into the opener, or -1 when the opener is not running.</summary>
    public int OpenerStep => OpenerActive ? _openerStep : -1;

    public bool OpenerActive =>
        settings.UseOpener
        && !_openerAborted
        && job.Opener is not null
        && _openerStep < job.Opener.Steps.Count;

    /// <summary>
    /// Rebuilds the GCD/off-global lookup from the job's action list. Must be called if any
    /// <see cref="ActionRef"/> was rebound after the session was created - normally id
    /// verification runs first and this is not needed.
    /// </summary>
    public void RefreshActionIds()
    {
        _oGcdIds = BuildKindSet(job, ActionKind.OGcd);
        _gcdIds = BuildKindSet(job, ActionKind.Gcd);
    }

    /// <summary>Resets all per-pull state. Called on job change, zone change, wipe.</summary>
    public void Reset()
    {
        _weavesThisWindow = 0;
        _lastGcdRemaining = 0f;
        _openerStep = 0;
        _openerAborted = false;
        _held = null;
        _heldSince = 0d;
    }

    /// <summary>
    /// Works out what the given button should be right now. Never returns null: if nothing
    /// matches, the player's own hotbar action is handed straight back.
    /// </summary>
    public Suggestion Resolve(RotationMode mode, CombatSnapshot snapshot, IActionState actions)
    {
        TrackGcdWindow(snapshot);
        TrackCombat(snapshot);

        var effectiveMode = mode;
        string? modeNote = null;

        // The AoE button on a single target is almost always a mistake, not an intent.
        if (mode == RotationMode.Aoe
            && settings.AoeFallsBackToSingleTarget
            && snapshot.EnemiesInAoeRange <= 1)
        {
            effectiveMode = RotationMode.SingleTarget;
            modeNote = "only one enemy, using single target";
        }

        var context = new RotationContext(snapshot, actions, settings, effectiveMode, _weavesThisWindow);
        var extra = ExtraFor(mode);

        var plan = extra?.Plan
            ?? (effectiveMode == RotationMode.Aoe ? job.Aoe : job.SingleTarget);

        var fallback = extra?.Host
            ?? (mode == RotationMode.Aoe ? job.AoeButton : job.SingleTargetButton);

        // An extra button drives its own sequence, so the global-cooldown split does not
        // apply unless the job asks for it: first matching rule simply wins.
        var fresh = extra is not null && !extra.RespectWeaveWindow
            ? ResolveSequence(context, plan, fallback)
            : ResolveFresh(context, plan, fallback, modeNote);
        var stabilised = Stabilise(fresh, context, snapshot.Now);

        // The prompt is decided after stabilisation so it always reflects the window the
        // player is actually looking at.
        stabilised.PotionPrompt = ShouldPromptPotion(context, stabilised);
        return stabilised;
    }

    /// <summary>The extra button for this mode, or null for the two main buttons.</summary>
    private ExtraButton? ExtraFor(RotationMode mode)
    {
        var index = mode switch
        {
            RotationMode.Extra1 => 0,
            RotationMode.Extra2 => 1,
            _ => -1,
        };

        return index >= 0 && index < job.ExtraButtons.Count ? job.ExtraButtons[index] : null;
    }

    /// <summary>
    /// Flat resolution for an extra button: the first rule whose action is usable and whose
    /// condition holds, with no global-cooldown gating.
    /// </summary>
    private static Suggestion ResolveSequence(
        RotationContext context,
        RotationPlan plan,
        ActionRef fallback)
    {
        foreach (var rule in plan.Rules)
        {
            var action = rule.Evaluate(context);
            if (action is not null)
                return new Suggestion(action, action, rule.NoteFor(context), rule.Positional);
        }

        return new Suggestion(fallback, fallback, "nothing to suggest");
    }

    private Suggestion ResolveFresh(
        RotationContext context,
        RotationPlan plan,
        ActionRef fallback,
        string? modeNote)
    {
        // A scripted opener overrides the priority list, but only while reality agrees.
        var openerAction = ResolveOpener(context);
        if (openerAction is not null)
            return new Suggestion(openerAction, openerAction, $"opener step {_openerStep + 1}");

        // Always work out the next GCD, even when we are going to suggest a weave. The HUD
        // shows it as a look-ahead, and it is the fallback when no weave fits.
        var gcdMatch = FirstMatch(plan, ActionKind.Gcd, context);
        var nextGcd = gcdMatch?.action;

        var positional = gcdMatch?.rule.Positional ?? PositionalHint.None;

        context.NextGcd = nextGcd;
        context.NextPositional = positional;

        if (context.CanWeave)
        {
            // A positional we cannot stand in is worth more than the next cooldown.
            var rescue = ResolvePositionalRescue(context, positional);
            if (rescue is not null)
                return new Suggestion(rescue, nextGcd, "positional rescue", positional);

            var oGcdMatch = FirstMatch(plan, ActionKind.OGcd, context);
            if (oGcdMatch is not null)
            {
                return new Suggestion(
                    oGcdMatch.Value.action,
                    nextGcd,
                    oGcdMatch.Value.rule.NoteFor(context) ?? "weave",
                    positional);
            }
        }

        if (nextGcd is not null)
        {
            var note = modeNote ?? gcdMatch?.rule.NoteFor(context);
            return new Suggestion(nextGcd, nextGcd, note, positional);
        }

        return new Suggestion(fallback, fallback, modeNote ?? "nothing to suggest");
    }

    /// <summary>
    /// Whether now is the moment to pop a potion. Requires a real weave window, so the
    /// prompt never asks for a press that would clip the global cooldown.
    /// </summary>
    private bool ShouldPromptPotion(RotationContext context, Suggestion suggestion)
    {
        if (!settings.PotionEnabled || !context.Snapshot.PotionAvailable)
            return false;

        if (!context.InCombat || context.Downtime || !context.CanWeave)
            return false;

        if (settings.PotionInOpener
            && OpenerActive
            && job.Opener is not null
            && job.Opener.PotionBeforeStep >= 0
            && _openerStep == job.Opener.PotionBeforeStep)
        {
            return true;
        }

        if (!settings.PotionOnBurst)
            return false;

        // Either the job's burst buff is up, or the button has just become the ability that
        // opens its burst - so the potion lands in the same window as the damage it buffs.
        if (job.BurstStatus is not null && context.Buff(job.BurstStatus))
            return true;

        return job.BurstAction is not null && suggestion.Action.Id == job.BurstAction.Id;
    }

    private static (Rule rule, ActionRef action)? FirstMatch(
        RotationPlan plan,
        ActionKind kind,
        RotationContext context)
    {
        var rules = plan.Rules;
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule.Kind != kind)
                continue;

            var action = rule.Evaluate(context);
            if (action is not null)
                return (rule, action);
        }

        return null;
    }

    private ActionRef? ResolvePositionalRescue(RotationContext context, PositionalHint positional)
    {
        if (!settings.SuggestPositionalRescue || positional == PositionalHint.None)
            return null;

        // Only rescue when the GCD is imminent - there is no point burning it two globals early.
        if (context.GcdRemaining > context.Settings.AssumedAnimationLock * 2f)
            return null;

        var standingCorrectly = positional switch
        {
            PositionalHint.Flank => context.Position == RelativePosition.Flank,
            PositionalHint.Rear => context.Position == RelativePosition.Rear,
            _ => true,
        };

        if (standingCorrectly || context.Position == RelativePosition.Unknown)
            return null;

        var rescue = job.PositionalRescue;
        if (rescue is null || !context.Ready(rescue))
            return null;

        if (job.PositionalRescueStatus is not null && context.Buff(job.PositionalRescueStatus))
            return null;

        return rescue;
    }

    private ActionRef? ResolveOpener(RotationContext context)
    {
        var opener = job.Opener;
        if (opener is null || !settings.UseOpener || _openerAborted)
            return null;

        if (!context.InCombat || context.Level < opener.MinimumLevel)
            return null;

        if (_openerStep >= opener.Steps.Count)
            return null;

        // Only ever start an opener at the start of a fight. Loading mid-pull, or joining a
        // fight in progress, must not rewind the button to step one.
        if (_openerStep == 0 && context.CombatDuration > settings.OpenerGraceSeconds)
        {
            _openerAborted = true;
            return null;
        }

        var step = opener.Steps[_openerStep];

        // If the scripted action is not usable the world has diverged from the script -
        // a late pull, a sync, a missing level. Drop out rather than jam the button.
        if (!context.Ready(step))
        {
            // An off-global that simply does not fit this window is not a divergence; wait.
            if (step.Kind == ActionKind.OGcd && !context.CanWeave)
                return null;

            _openerAborted = true;
            return null;
        }

        if (step.Kind == ActionKind.OGcd && !context.CanWeave)
            return null;

        return step;
    }

    /// <summary>
    /// Holds a suggestion briefly so the icon cannot flicker between two actions while
    /// somebody is mid-reach for the key. The hold is dropped the moment the held action
    /// stops being usable, so it can never cause a dud press.
    /// </summary>
    private Suggestion Stabilise(Suggestion fresh, RotationContext context, double now)
    {
        if (settings.SuggestionHoldSeconds <= 0f)
        {
            _held = fresh;
            _heldSince = now;
            return fresh;
        }

        if (_held is not null
            && _held.Action.Id != fresh.Action.Id
            && now - _heldSince < settings.SuggestionHoldSeconds
            && context.Ready(_held.Action)
            && (_held.Kind == ActionKind.Gcd || context.CanWeave))
        {
            return _held;
        }

        if (_held is null || _held.Action.Id != fresh.Action.Id)
        {
            _held = fresh;
            _heldSince = now;
        }

        return fresh;
    }

    /// <summary>
    /// Told by the plugin when an action actually goes off, so the weave budget and the
    /// opener stay in step with what the player really did.
    /// </summary>
    public void NotifyActionUsed(uint actionId)
    {
        if (_gcdIds.Contains(actionId))
            _weavesThisWindow = 0;
        else if (_oGcdIds.Contains(actionId))
            _weavesThisWindow++;

        var opener = job.Opener;
        if (opener is null || _openerAborted || _openerStep >= opener.Steps.Count)
            return;

        if (opener.Steps[_openerStep].Id == actionId)
            _openerStep++;
        else
            _openerAborted = true;
    }

    private void TrackGcdWindow(CombatSnapshot snapshot)
    {
        // The GCD jumping back up means a fresh window: reset the weave budget. This is a
        // belt-and-braces backup for NotifyActionUsed, which can miss an action if the
        // plugin was loaded mid-fight.
        if (snapshot.GcdRemaining > _lastGcdRemaining + 0.05f)
            _weavesThisWindow = 0;

        _lastGcdRemaining = snapshot.GcdRemaining;
    }

    private void TrackCombat(CombatSnapshot snapshot)
    {
        if (snapshot.InCombat == _wasInCombat)
            return;

        _wasInCombat = snapshot.InCombat;

        // Leaving or entering combat rearms the opener for the next pull.
        _openerStep = 0;
        _openerAborted = false;
        _weavesThisWindow = 0;
    }

    private static HashSet<uint> BuildKindSet(IJobRotation job, ActionKind kind)
    {
        var set = new HashSet<uint>();
        foreach (var action in job.AllActions)
        {
            if (action.Kind == kind)
                set.Add(action.Id);
        }

        return set;
    }
}
