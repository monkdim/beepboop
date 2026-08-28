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

    /// <summary>
    /// Why the opener stopped driving, or null while it still is. The opener giving up was
    /// silent, and a recorded pull that stops being driven at step seven looks exactly like
    /// one that ran to the end - so two logs told us it happened and neither could say why.
    /// </summary>
    public string? OpenerOutcome { get; private set; }

    /// <summary>
    /// Why the opener declined to answer on the last frame it was asked. Aborting is only
    /// one of the ways it can stop driving, and it turned out not to be the one that
    /// happens: a recorded pull stopped at step seven with no abort recorded at all, which
    /// means the opener was returning nothing for one of the quiet reasons below and doing
    /// it every frame after. Constant strings, because this is set inside the frame loop.
    /// </summary>
    private string? _openerDecline;

    /// <summary>
    /// What the opener did on the pull that just ended, kept across the rearm that leaving
    /// combat performs.
    /// <para>
    /// This is why the reason line never appeared. Leaving combat rewinds the opener for the
    /// next pull - step back to zero, abort cleared, reason cleared - and recording is
    /// stopped *after* the fight ends. So every recorded pull asked the opener what it had
    /// done and was answered by a freshly rearmed one that had done nothing. Three logs in a
    /// row printed no line at all, including a Dragoon pull whose opener plainly aborted on
    /// the first global.
    /// </para>
    /// </summary>
    public string? LastOpenerReport { get; private set; }

    /// <summary>The report a recorded log should carry: the live one, or the last pull's.</summary>
    public string? OpenerReportForLog => OpenerReport ?? LastOpenerReport;

    /// <summary>How many times leaving combat rewound the opener during this session.</summary>
    private int _openerRewinds;

    /// <summary>Where the opener got to and what it is doing, for the recorded log.</summary>
    public string? OpenerReport
    {
        get
        {
            if (job.Opener is null || _openerStep == 0 && _openerDecline is null && OpenerOutcome is null)
                return null;

            var where = $"step {_openerStep + 1} of {job.Opener.Steps.Count}";

            if (OpenerOutcome is not null)
                return $"{where}, gave up: {OpenerOutcome}";

            return _openerDecline is null
                ? $"{where}, still driving"
                : $"{where}, held there: {_openerDecline}";
        }
    }
    private bool _wasInCombat;

    /// <summary>
    /// The held suggestion per button, not per session.
    /// <para>
    /// The game asks what every hotbar slot should cast every frame, so with both buttons
    /// on the bar the two modes are resolved one after the other, over and over. A single
    /// held suggestion shared between them made each button hand its answer to the other:
    /// the AoE button would return the single-target ability, the single-target button
    /// would return it back, and the pair swapped every hold window. Reported as "the
    /// button just starts changing rapidly without being pressed", and as never seeing the
    /// AoE rotation at all - both rows of the display were showing the same list.
    /// </para>
    /// </summary>
    private readonly Dictionary<RotationMode, (Suggestion Suggestion, double Since)> _held = [];

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
        OpenerOutcome = null;
        _openerDecline = null;
        LastOpenerReport = null;
        _openerRewinds = 0;
        _held.Clear();
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
        var stabilised = Stabilise(fresh, context, snapshot.Now, mode);

        // The prompt is decided after stabilisation so it always reflects the window the
        // player is actually looking at.
        stabilised.PotionPrompt = ShouldPromptPotion(context, stabilised);

        // Set after stabilisation and every frame, so the display tracks the player moving
        // even while the suggestion itself is being held steady.
        stabilised.Position = snapshot.Position;
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

        if (context.Downtime)
            return false;

        var atOpenerPotionStep = settings.PotionInOpener
            && OpenerActive
            && job.Opener is not null
            && job.Opener.PotionBeforeStep >= 0
            && _openerStep == job.Opener.PotionBeforeStep;

        // Before the pull there is no global rolling, so a potion cannot clip anything.
        // Several of the Balance openers drink a few seconds early for exactly that
        // reason, and the prompt is useless if it only appears once the fight has started.
        if (!context.InCombat)
            return atOpenerPotionStep;

        if (!context.CanWeave)
            return false;

        if (atOpenerPotionStep)
            return true;

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

        if (context.Level < opener.MinimumLevel)
        {
            _openerDecline = "the level is below the one the opener is written for";
            return null;
        }

        if (_openerStep >= opener.Steps.Count)
        {
            _openerDecline = "the sequence ran to its end";
            return null;
        }

        // Only ever start an opener at the start of a fight. Loading mid-pull, or joining a
        // fight in progress, must not rewind the button to step one.
        if (context.InCombat && _openerStep == 0 && context.CombatDuration > settings.OpenerGraceSeconds)
        {
            Abort($"the fight was already {context.CombatDuration:0.0}s old at the first step");
            return null;
        }

        // An off-global whose own cooldown is still turning is not the world diverging from
        // the script - it is a cooldown that was already running when the pull started,
        // which on a striking dummy is most of them. Two recorded pulls died here: Dragoon
        // lost the whole opener at step three because Lance Charge was still down from the
        // previous attempt, and Black Mage the same at step four over Amplifier. Twenty-one
        // scripted steps thrown away for one missing weave.
        //
        // So skip it and carry on. The globals are the backbone of an opener, and the
        // priority list will weave the ability the moment it comes back. A step that is
        // merely not usable this instant - a proc that has not landed, a window that has
        // not opened - is still waited for; only a real cooldown is stepped over.
        while (_openerStep < opener.Steps.Count)
        {
            var candidate = opener.Steps[_openerStep];

            if (candidate.Kind != ActionKind.OGcd
                || context.Ready(candidate)
                || context.Cd(candidate) <= context.GcdTotal)
            {
                break;
            }

            _openerStep++;
        }

        if (_openerStep >= opener.Steps.Count)
        {
            _openerDecline = "the sequence ran to its end";
            return null;
        }

        var step = opener.Steps[_openerStep];

        // Out of combat nothing is rolling, so an off-global cannot clip a global and the
        // weave budget does not apply. Most of the Balance openers start before the pull -
        // Meikyo Shisui, Reassemble, a prepull Harpe - and none of that is reachable if
        // weaving rules written for a live fight are applied to a standing start.
        var canWeave = context.CanWeave || !context.InCombat;

        // If the scripted action is not usable the world has diverged from the script -
        // a late pull, a sync, a missing level. Drop out rather than jam the button.
        if (!context.Ready(step))
        {
            // An off-global that simply does not fit this window is not a divergence; wait.
            if (step.Kind == ActionKind.OGcd && !canWeave)
            {
                _openerDecline = "the next step is an off-global and no weave slot is open";
                return null;
            }

            // Nor is anything before the pull: there is no target, no gauge and no buff to
            // diverge from yet. Wait for the fight rather than burning the opener.
            if (!context.InCombat)
            {
                _openerDecline = "the next step is not usable and the fight has not started";
                return null;
            }

            // Nor is a cast in flight. This is the one that was actually happening: the game
            // refuses every action while you are casting, and asking "is the next step
            // usable" during the cast of the step before it comes back no. Two recorded
            // pulls name it outright - "step 8 (Fire IV) was not usable" and "step 1 (Fire
            // III) was not usable" - and both died on the first hard cast the opener asked
            // for and got. You cannot go off script by doing exactly what the script said.
            if (context.Casting)
            {
                _openerDecline = "a cast is in flight, so the game refuses everything";
                return null;
            }

            Abort($"step {_openerStep + 1} ({step.Name}) was not usable");
            return null;
        }

        if (step.Kind == ActionKind.OGcd && !canWeave)
        {
            _openerDecline = "the next step is an off-global and no weave slot is open";
            return null;
        }

        _openerDecline = null;
        return step;
    }

    /// <summary>
    /// Holds a suggestion briefly so the icon cannot flicker between two actions while
    /// somebody is mid-reach for the key. The hold is dropped the moment the held action
    /// stops being usable, so it can never cause a dud press.
    /// </summary>
    private Suggestion Stabilise(
        Suggestion fresh,
        RotationContext context,
        double now,
        RotationMode mode)
    {
        if (settings.SuggestionHoldSeconds <= 0f)
        {
            _held[mode] = (fresh, now);
            return fresh;
        }

        var known = _held.TryGetValue(mode, out var current);

        if (known
            && current.Suggestion.Action.Id != fresh.Action.Id
            && now - current.Since < settings.SuggestionHoldSeconds
            && context.Ready(current.Suggestion.Action)
            && (current.Suggestion.Kind == ActionKind.Gcd || context.CanWeave))
        {
            return current.Suggestion;
        }

        if (!known || current.Suggestion.Action.Id != fresh.Action.Id)
            _held[mode] = (fresh, now);

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

        // Weaves are not strictly ordered in practice: an opener chart draws two off-globals
        // in one window, and which goes first inside that window does not matter. Nor does
        // pressing the next global while an off-global step is still waiting for a slot.
        // Neither is going off script, so look ahead as far as the next global and accept a
        // match anywhere in between - the steps stepped over are weaves that will not
        // happen, and the priority list picks those up.
        for (var i = _openerStep; i < opener.Steps.Count; i++)
        {
            if (opener.Steps[i].Id == actionId)
            {
                _openerStep = i + 1;
                return;
            }

            // Past the first global that is not the one used, this really is a different
            // rotation and the opener has no business driving it.
            if (opener.Steps[i].Kind == ActionKind.Gcd)
                break;
        }

        if (_wasInCombat)
        {
            Abort($"step {_openerStep + 1} wanted {opener.Steps[_openerStep].Name}, "
                  + $"but {NameOf(actionId)} was used");
        }
        else
        {
            // A stray press while waiting for the pull is somebody fidgeting on a dummy,
            // not a divergence worth throwing the opener away over. Start the pre-pull
            // walk again instead.
            _openerStep = 0;
        }
    }

    /// <summary>
    /// The job's own name for an action id. The abort reason is read by a person out of a
    /// recorded log, and "action 154 was used" makes them go and look up 154.
    /// </summary>
    private string NameOf(uint actionId)
    {
        var all = job.AllActions;
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Id == actionId)
                return all[i].Name;
        }

        return $"action {actionId}";
    }

    private void Abort(string why)
    {
        _openerAborted = true;
        OpenerOutcome = why;
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
        _weavesThisWindow = 0;

        // Leaving combat rearms the opener for the next pull. Entering it must not: by then
        // the pre-pull steps have already been walked, and rewinding here would ask for
        // them a second time with the fight already running.
        if (!snapshot.InCombat)
        {
            // Keep what it did before wiping it. A rewind mid-pull is also worth counting:
            // combat dropping for a moment sends the step back to zero, and the guard against
            // starting an opener late then ends it for good on the very next frame.
            var report = OpenerReport;
            if (report is not null)
            {
                _openerRewinds++;
                LastOpenerReport = _openerRewinds > 1
                    ? $"{report} (combat ended {_openerRewinds} times, rewinding it each time)"
                    : report;
            }

            _openerStep = 0;
            _openerAborted = false;
            OpenerOutcome = null;
            _openerDecline = null;
        }
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
