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

    /// <summary>
    /// Drops the carried report, so a recording can only ever print one from inside itself.
    /// <para>
    /// The belt to the braces above. A session outlives any number of recordings - a whole
    /// evening of duties - and a footer that describes a pull the reader was not looking at
    /// is worse than no footer, because it reads as a finding. Two synced dungeon logs said
    /// "held there: Brotherhood is still on cooldown" for a Monk opener that is written for
    /// level 100 and could not have been consulted at either level.
    /// </para>
    /// </summary>
    public void ForgetOpenerReport() => LastOpenerReport = null;

    /// <summary>How many times leaving combat rewound the opener during this session.</summary>
    private int _openerRewinds;

    /// <summary>
    /// Whether this run of the opener has already stepped over a global it could not cast.
    /// <para>
    /// One is tolerated, a run of them is not. The chart's eighth global is Xenoglossy, and
    /// the Polyglot it spends comes from the Amplifier woven into its second - so a pull
    /// that begins with Amplifier still turning from the last attempt reaches that global
    /// with nothing to spend, and the whole rest of the chart went with it. What the player
    /// sees is the priority list's own fire, ice, fire, fire in place of the opener.
    /// </para>
    /// </summary>
    private bool _openerSteppedOverAGlobal;

    /// <summary>
    /// The actions this session has suggested since the last one actually went off, so an
    /// abort can tell a player who went off script from one who did exactly what the button
    /// said.
    /// <para>
    /// A set rather than a single id: both buttons are resolved every frame and either may
    /// be the one pressed. It is cleared the moment an action goes off, so it only ever
    /// holds what was genuinely on offer leading up to that press.
    /// </para>
    /// </summary>
    private readonly HashSet<uint> _suggestedSinceLastUse = [];

    /// <summary>
    /// The step the settle clock below is running for, and when it started. -1 when no step
    /// has been looked at yet.
    /// </summary>
    private int _openerSettlingStep = -1;
    private double _openerStepSince;

    /// <summary>
    /// How long a step is given to become usable before it is judged at all.
    /// <para>
    /// The opener asks the game about the next step on every frame, including the frames
    /// immediately after the step before it went off - and for a moment the game has not
    /// caught up: the action is away, the combo, gauge or buff it grants has not landed
    /// yet, and everything that depends on it answers no. Judged on that frame the world
    /// has diverged from the script, when all that has happened is a round trip.
    /// </para>
    /// <para>
    /// Two recorded Viper pulls are the whole story. The chart's fifth global is Vicewinder
    /// and its sixth is Hunter's Coil, which is only usable once Vicewinder's chain shows in
    /// the gauge. Both pulls asked about Hunter's Coil on the frame after Vicewinder, got
    /// no, stepped over it, and then gave up on the Twinfang Bite behind it - twenty-one
    /// scripted steps thrown away for one frame of latency.
    /// </para>
    /// <para>
    /// Waiting costs nothing: the opener answers nothing during the settle, so the priority
    /// list drives, which is exactly what it would have done had the opener given up.
    /// </para>
    /// </summary>
    private const float StepSettleSeconds = 0.75f;

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

    /// <summary>
    /// Off-globals per window: the player's setting, raised to the job's minimum where the
    /// job asks for one. "Globals only" is left alone - a minimum raises a setting that
    /// already allows weaving, it never turns weaving on.
    /// </summary>
    public int WeaveBudget =>
        settings.MaxWeavesPerGcd <= 0
            ? 0
            : Math.Max(settings.MaxWeavesPerGcd, (int)job.MinimumWeaveStyle);

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
        _openerSteppedOverAGlobal = false;
        _openerSettlingStep = -1;
        _suggestedSinceLastUse.Clear();
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

        // The AoE button on too few enemies is almost always a mistake, not an intent. How
        // few is the job's to say: most area combos are a gain at two, Viper's at three.
        if (mode == RotationMode.Aoe
            && settings.AoeFallsBackToSingleTarget
            && snapshot.EnemiesInAoeRange < job.AoeMinimumEnemies)
        {
            effectiveMode = RotationMode.SingleTarget;
            modeNote = snapshot.EnemiesInAoeRange <= 1
                ? "only one enemy, using single target"
                : $"only {snapshot.EnemiesInAoeRange} enemies, using single target";
        }

        var context = new RotationContext(
            snapshot, actions, settings, effectiveMode, _weavesThisWindow, WeaveBudget);
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

        // Recorded so the opener can tell "the player went off script" from "the player
        // pressed the button and got what we had put on it". See NotifyActionUsed.
        if (stabilised.Action.Id != 0)
            _suggestedSinceLastUse.Add(stabilised.Action.Id);

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

        // Before the pull, with a script still standing by, an off-global out of the priority
        // list is a cooldown thrown away.
        //
        // The opener declines for a frame or two at a standing start - the game refuses
        // everything during the animation lock of the global before, so "is step two usable"
        // comes back no - and the priority list answers into that gap. A recorded Monk pull
        // shows what that costs: Dragon Kick as opener step one at 00:03.2, then Brotherhood
        // out of the priority list at 00:03.8, still out of combat. The chart puts Brotherhood
        // at step five, beside Riddle of Fire; spending it 1.8s before the pull threw away
        // that much of a twenty second raid buff and left it 5.6s out of phase with Riddle of
        // Fire for the rest of the fight, because both are then used on cooldown from where
        // they first went off.
        //
        // Only while the opener is still standing by, and only off-globals: the global the
        // list picks pre-pull is the one that starts the fight, and it is the same one the
        // opener is about to ask for anyway.
        var scriptStandingBy = OpenerActive && !context.InCombat;

        if (context.CanWeave && !scriptStandingBy)
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

        // "Nothing to suggest" has two very different causes and the log could not tell them
        // apart. One is a list with no opinion, which is a rotation problem. The other is the
        // game refusing everything for a frame, which is not - and which a recorded pull
        // showed once in thirty-eight thousand asks, with the combo starter itself refused.
        var fallbackNote = modeNote
            ?? (context.Ready(fallback)
                ? "nothing to suggest"
                : $"the game refused everything, {fallback.Name} included");

        return new Suggestion(fallback, fallback, fallbackNote);
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

        // Only rescue once the global is close enough that there is no time left to walk.
        //
        // This asked whether a weave would fit - AssumedAnimationLock doubled, so 1.3 seconds
        // - and used the answer to decide whether the player could still reposition. They are
        // not the same question, and a second and a third of a second is a long time to a
        // player who was already halfway round the boss. Reported as "the facing was right but
        // it fires a little prematurely because I could easily make it in time", from a pull
        // where True North went out four times in two and a half minutes on a two charge
        // cooldown - full uptime, and so nothing banked for the moment it is really needed.
        //
        // Never tighter than a weave actually fits, whatever the setting says. CanWeave above
        // is the real gate and enforces the same floor, so this only stops the setting from
        // promising a rescue the engine would then decline to offer.
        var window = Math.Max(
            settings.PositionalRescueWindow,
            settings.AssumedAnimationLock + settings.WeaveSafetyMargin);

        if (context.GcdRemaining > window)
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
        // The opener is a single-target chart and it drives the single-target button only.
        //
        // It used to answer both, because this never looked at which button was asking. A
        // recorded level 100 dungeon has the player pressing the *area* button into a six
        // target pull and being walked through Twin Snakes, Demolish, Leaping Opo, Dragon
        // Kick, Leaping Opo - five single target globals on six enemies - with Rockbreaker
        // sitting right there in the other list.
        //
        // Read off the mode the context was built with, not the one the caller asked for, so
        // the area button still shows the opener when it has fallen back to single target on
        // one enemy - which is the same fallback that makes that button safe to hold down.
        //
        // Returns before the line below, so a frame spent answering the area button neither
        // writes a reason nor clears the one the single-target frame wrote.
        if (context.Mode != RotationMode.SingleTarget)
            return null;

        // Cleared first so every path below writes its own reason and none can be read as
        // the reason for a frame it was not decided on. Two recorded synced dungeons had
        // the log naming a cooldown for a stand-down that was really about level.
        _openerDecline = null;

        var opener = job.Opener;
        if (opener is null || _openerAborted)
            return null;

        if (!settings.UseOpener)
        {
            _openerDecline = "the opener is switched off in the settings";
            return null;
        }

        // Named with both numbers. "The level is below the one it is written for" is true of
        // a level 99 pull and of a level 60 one, and reading a synced log you want to know
        // which without going and looking the opener up.
        if (context.Level < opener.MinimumLevel)
        {
            _openerDecline =
                $"the opener is written for level {opener.MinimumLevel} and you are {context.Level}";
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

        // An opener whose burst cooldown is still turning is not an opener. It is the priority
        // list with the resource management taken out - and worse than that, because the
        // chart spends Perfect Balance, Meikyo Shisui, whatever the job banks, into a window
        // that has no buffs in it.
        //
        // A recorded alliance raid is the whole case. Combat ends ten times across nineteen
        // minutes - every trash pack, every boss boundary - and the opener re-arms each time.
        // Twice it got going with Brotherhood and Riddle of Fire both still down, stepped over
        // exactly those two steps because they were not ready, and drove Perfect Balance
        // straight into a Blitz with nothing on it: a Phantom Rush at 07:34.0 and a Rising
        // Phoenix at 14:41.8, the only two naked Blitzes in twenty-two. The second one spent
        // the charge eighteen seconds after the priority list had correctly saved the first
        // one for a damage window.
        //
        // Out of combat this waits rather than gives up: standing on a boss waiting for a
        // two minute cooldown is exactly when the opener is worth having. Once the fight is
        // running without it, it is not happening.
        if (_openerStep == 0
            && job.BurstAction is { } burst
            && context.Has(burst)
            && !context.Ready(burst)
            && context.Cd(burst) > context.GcdTotal)
        {
            if (!context.InCombat)
            {
                _openerDecline = $"{burst.Name} is still on cooldown";
                return null;
            }

            Abort($"{burst.Name} was still on cooldown at the first step");
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

            // Start the settle clock the first time this step is looked at. A cooldown
            // reading is immediate and honest, so the weave skip below does not wait on it;
            // everything that asks "is this usable" does.
            if (_openerSettlingStep != _openerStep)
            {
                _openerSettlingStep = _openerStep;
                _openerStepSince = context.Snapshot.Now;
            }

            var settled = context.Snapshot.Now - _openerStepSince >= StepSettleSeconds;

            // A weave whose own cooldown is still turning is not the world diverging.
            if (candidate.Kind == ActionKind.OGcd
                && !context.Ready(candidate)
                && context.Cd(candidate) > context.GcdTotal)
            {
                _openerStep++;
                continue;
            }

            // Nor is a single global the chart assumed a resource for. Skipping the weave
            // above is what takes that resource away, so refusing to skip the global it paid
            // for turns one missing weave into a missing opener. Only ever one, and only
            // once the fight is running and nothing is in flight - anything more and the
            // priority list is the better answer.
            if (candidate.Kind == ActionKind.Gcd
                && settled
                && !_openerSteppedOverAGlobal
                && context.InCombat
                && !context.Casting
                && !context.Ready(candidate)
                && _openerStep + 1 < opener.Steps.Count)
            {
                _openerSteppedOverAGlobal = true;
                _openerStep++;
                continue;
            }

            break;
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

            // Nor is a step the game has not caught up to yet - see StepSettleSeconds.
            if (context.Snapshot.Now - _openerStepSince < StepSettleSeconds)
            {
                _openerDecline = "the step before it has only just gone off";
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
        // Taken before the set is cleared: this press is judged against what was on offer
        // leading up to it, not against whatever gets suggested after it.
        var wasOurs = _suggestedSinceLastUse.Contains(actionId);
        _suggestedSinceLastUse.Clear();

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

        // Whatever this was, it was not the opener's idea - had it been, the loop above would
        // have matched it and returned. So the last question worth asking before throwing
        // thirty-odd scripted steps away is whether it was nonetheless *our* idea.
        //
        // It very often is. The opener stands down for perfectly ordinary reasons - an
        // off-global step with no weave slot open, a step the game calls unusable before the
        // pull - and returns null, and the priority list answers that global instead. The
        // player presses the button, gets what we had put on it, and the opener aborts over
        // its own plugin's suggestion. A recorded Red Mage pull dies exactly there: "step 3
        // of 36, gave up: step 3 wanted Swiftcast, but Veraero III was used" - and Veraero
        // III is what the button was showing at that moment, with the priority list's own
        // reason printed beside it in the same log.
        //
        // The method below already says this for the sibling case: you cannot go off script
        // by doing exactly what the script said. It is no less true when the script was
        // standing down and something else of ours was doing the talking.
        //
        // Held rather than aborted, so the step stays where it is: the weave slot opens, the
        // cooldown comes back, and the opener picks up. The log then reads "held there"
        // instead of "gave up", which is also what actually happened.
        // Ours: hold, wherever we are. The step stays exactly where it is, which is what the
        // note above promises and what this quietly did not do. Mid-fight the rewind was
        // worse than aborting, because the guard that refuses to start an opener late then
        // fires on the very next frame and the log blames the wrong thing entirely: a
        // recorded Monk pull ran five steps cleanly, took one priority-list global while the
        // opener stood down waiting for a weave slot, and reported "step 1 of 20, gave up:
        // the fight was already 4.9s old at the first step" - a rewind wearing a late-start's
        // clothes.
        //
        // Before the pull it was quieter and no less wrong. A later pull walked Dragon Kick
        // as step one, took a priority-list Brotherhood 0.6s behind it while the opener was
        // still settling, and went back to step one - so the chart's own Dragon Kick was
        // pressed twice and Brotherhood, which belongs at step five, had already gone.
        if (wasOurs)
            return;

        if (_wasInCombat)
        {
            Abort($"step {_openerStep + 1} wanted {opener.Steps[_openerStep].Name}, "
                  + $"but {NameOf(actionId)} was used");
            return;
        }

        // A stray press while waiting for the pull is somebody fidgeting on a dummy, not a
        // divergence worth throwing the opener away over. Start the pre-pull walk again.
        _openerStep = 0;
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
            else
            {
                // Assigned either way. Held only when it had something to say, it survived
                // the pull it belonged to and turned up in the footer of a later one.
                LastOpenerReport = null;
            }

            _openerStep = 0;
            _openerAborted = false;
            OpenerOutcome = null;
            _openerDecline = null;
            _openerSteppedOverAGlobal = false;
            _openerSettlingStep = -1;
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
