using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using OneTwoPunch.Plugin.Services;

namespace OneTwoPunch.Plugin;

/// <summary>
/// The whole trick, and the reason this is not an automation plugin.
/// <para>
/// The game asks <c>GetAdjustedActionId</c> what a hotbar slot should really cast - that is
/// how one button becomes Heavens' Thrust after another. We answer that question, and
/// nothing else. The icon updates, and the player still presses the key. One keypress is
/// still exactly one action; nothing is queued, timed or sent on anybody's behalf.
/// </para>
/// <para>
/// The hook is built the first time it is enabled rather than at construction, and it is
/// only ever enabled once the player is actually in the world. Plugins load during the
/// game's own startup, and installing a hook into a subsystem that is still assembling
/// itself is a good way to take the whole process down. Nothing here touches the game
/// until there is a character standing in it.
/// </para>
/// </summary>
public sealed unsafe class ActionReplacer : IDisposable
{
    private readonly IGameInteropProvider _interop;
    private readonly IPluginLog _log;

    private readonly Func<uint, RotationMode?> _classify;
    private readonly Func<RotationMode, Suggestion?> _resolve;

    private Hook<ActionManager.Delegates.GetAdjustedActionId>? _hook;

    /// <summary>Set when hooking has failed, so it is attempted once and not every frame.</summary>
    private bool _unavailable;

    /// <summary>Set once the "not resolved yet" warning has been written.</summary>
    private bool _warnedUnresolved;

    public ActionReplacer(
        IGameInteropProvider interop,
        IPluginLog log,
        Func<uint, RotationMode?> classify,
        Func<RotationMode, Suggestion?> resolve)
    {
        _interop = interop;
        _log = log;
        _classify = classify;
        _resolve = resolve;
    }

    /// <summary>True once the hook exists and the plugin is answering the game.</summary>
    public bool IsActive => _hook is { IsEnabled: true };

    /// <summary>
    /// Builds the hook if it does not exist yet, then enables it. Safe to call repeatedly.
    /// Returns false if the hook could not be established, in which case the plugin simply
    /// does nothing rather than risking the game.
    /// </summary>
    public bool Enable()
    {
        if (_unavailable)
            return false;

        try
        {
            if (_hook is null)
            {
                // Hooked through FFXIVClientStructs' own resolved address and its own
                // generated delegate type, which is how BossMod hooks this subsystem.
                // Addresses.X is the canonical location of the function; Delegates.X is
                // generated from the same declaration the game's own signature is matched
                // against, so the parameter list and calling convention cannot drift from a
                // hand-written one. Resolved by signature rather than a fixed offset, so a
                // patch that shifts code around does not silently break it.
                //
                // If it has not resolved there is nothing to hook, and hooking address zero
                // would be catastrophic - so check before, not after.
                var address = ActionManager.Addresses.GetAdjustedActionId.Value;
                if (address == 0)
                {
                    // Enable() is retried every frame until it succeeds, so this must not
                    // log every frame: a line per frame is a write to the log file sixty
                    // times a second, which costs far more than the thing it is reporting.
                    if (!_warnedUnresolved)
                    {
                        _warnedUnresolved = true;
                        _log.Warning("One Two Punch: GetAdjustedActionId is not resolved yet; will retry quietly.");
                    }

                    return false;
                }

                _hook = _interop.HookFromAddress<ActionManager.Delegates.GetAdjustedActionId>(address, Detour);
                _log.Information("One Two Punch: hooked GetAdjustedActionId at 0x{Address:X}.", address);
            }

            if (!_hook.IsEnabled)
                _hook.Enable();

            return true;
        }
        catch (Exception ex)
        {
            // Never retry a hook that has already failed once.
            _unavailable = true;
            _log.Error(ex, "One Two Punch: could not hook GetAdjustedActionId; the plugin is inert.");
            return false;
        }
    }

    public void Disable()
    {
        try
        {
            if (_hook is { IsEnabled: true })
                _hook.Disable();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "One Two Punch: could not disable the hook.");
        }
    }

    /// <summary>
    /// True while this thread is already inside the detour.
    /// <para>
    /// This is the difference between a plugin and a frozen game, and it is worth being
    /// plain about why. Working out what to suggest means asking the game questions -
    /// GetActionStatus, GetRecastTime, GetMaxCharges, GetActionInRangeOrLoS - and every one
    /// of those resolves the action's upgrade first, by calling GetAdjustedActionId. Which
    /// is the function this hook sits on. So answering the question re-asks it, and each
    /// re-ask asks again: unbounded recursion, one core pinned, the game thread never
    /// returning.
    /// </para>
    /// <para>
    /// Caching the answer per frame does not help, because the cache is only written once
    /// the answer exists and the recursion happens while computing it. The only thing that
    /// works is refusing to re-enter: a nested call is the game asking on our behalf, and it
    /// wants the game's own answer, not ours.
    /// </para>
    /// <para>
    /// Thread static rather than a plain field: the game may ask from more than one thread,
    /// and one thread's work must not suppress another's.
    /// </para>
    /// </summary>
    [ThreadStatic]
    private static bool _inDetour;

    private long _suppressed;
    private bool _reportedSuppression;

    /// <summary>
    /// How many nested calls have been turned away. Non-zero means the recursion this guard
    /// exists to stop is genuinely happening on this machine.
    /// </summary>
    public long SuppressedReentrantCalls => _suppressed;

    /// <summary>
    /// How many times the game has asked about one of our two buttons off its own bat.
    /// <para>
    /// Only asks from outside our own work are counted, and that distinction is the whole
    /// point of the number. Working out what to suggest means asking the game about the
    /// actions in the priority list, and the host action is in that list - so every frame's
    /// resolve asks about our own button, through this same hook. Counting those made the
    /// figure exactly one per frame whether or not the hotbar was drawing anything, which is
    /// the question it was added to answer. It reported 9248 asks over a 160 second pull and
    /// meant nothing at all.
    /// </para>
    /// </summary>
    public long TimesAsked => _asked;

    /// <summary>How many of those we answered with a replacement.</summary>
    public long TimesAnswered => _answered;

    /// <summary>
    /// Asks that arrived while we were working out an answer, rather than from the game
    /// asking on its own. Kept separate rather than dropped, so the split is visible.
    /// </summary>
    public long TimesAskedByOurOwnWork => _askedByUs;

    /// <summary>The id of the last replacement handed back, for the status line.</summary>
    public uint LastAnswer => _lastAnswer;

    private long _asked;
    private long _answered;
    private long _askedByUs;
    private uint _lastAnswer;

    /// <summary>
    /// True while the plugin is resolving a suggestion of its own accord rather than because
    /// the game asked. Thread static for the same reason <see cref="_inDetour"/> is.
    /// </summary>
    [ThreadStatic]
    private static bool _inOwnWork;

    /// <summary>
    /// Marks the work the plugin starts itself - the per-frame resolve - so the asks it
    /// causes are not mistaken for the game asking. Use with <c>using</c>.
    /// </summary>
    public static OwnWorkScope OwnWork() => new();

    public readonly ref struct OwnWorkScope
    {
        private readonly bool _previous;

        public OwnWorkScope()
        {
            _previous = _inOwnWork;
            _inOwnWork = true;
        }

        public void Dispose() => _inOwnWork = _previous;
    }

    /// <summary>
    /// The form the game currently gives an action - the upgrade the player has learned.
    /// <para>
    /// Every combo starter that upgrades made the plugin look completely dead on that job.
    /// Machinist's button is Split Shot, but from level 54 the hotbar slot carries Heated
    /// Split Shot, and the id the game asks about is the one on the slot. The button was
    /// never recognised, so it was never replaced - and Red Mage, Bard, Monk, Samurai and
    /// Summoner all have the same shape.
    /// </para>
    /// <para>
    /// Asked of the game rather than written down here, because a table of upgrades is a
    /// table that goes stale on a patch. This is the same call the answer already goes back
    /// through, and it is called a handful of times per level rather than per frame.
    /// </para>
    /// </summary>
    public uint CurrentFormOf(uint actionId)
    {
        var hook = _hook;
        if (hook is null || !hook.IsEnabled)
            return actionId;

        try
        {
            var manager = ActionManager.Instance();
            return manager is null ? actionId : hook.Original(manager, actionId);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "One Two Punch: could not resolve the current form of action {Id}", actionId);
            return actionId;
        }
    }

    private uint Detour(ActionManager* actionManager, uint actionId)
    {
        var hook = _hook;
        if (hook is null)
            return actionId;

        // Already inside: this call is one of ours, asked on our behalf. Answer it with the
        // game's own adjustment and do not start over.
        if (_inDetour)
        {
            // Counted, and reported once. Without the guard this is the path that recursed
            // without end, so seeing a number here is the confirmation that it was real.
            _suppressed++;
            if (!_reportedSuppression)
            {
                _reportedSuppression = true;
                _log.Information(
                    "One Two Punch: nested GetAdjustedActionId suppressed. This is expected - "
                    + "asking the game about an action makes it resolve that action's id.");
            }

            return hook.Original(actionManager, actionId);
        }

        _inDetour = true;
        try
        {
            var mode = _classify(actionId);
            if (mode is not null)
            {
                // Counted because "the button fires the right ability but the icon never
                // changes" is otherwise impossible to tell apart from "the game never asks".
                // The hotbar draws itself from this same function, so a slot whose icon is
                // frozen is a slot the game is not asking about - and that is a different
                // problem from a wrong answer.
                //
                // Only when the game asked of its own accord. Our own resolve asks about the
                // host action too, once a frame, and counting that made the number say "one
                // per frame" no matter what the hotbar was doing.
                if (_inOwnWork)
                    _askedByUs++;
                else
                    _asked++;

                var suggestion = _resolve(mode.Value);
                if (suggestion is not null && suggestion.Action.Id != 0)
                {
                    _lastAnswer = suggestion.Action.Id;
                    if (!_inOwnWork)
                        _answered++;

                    // Hand the answer back through the game's own adjustment so upgrades and
                    // procs still resolve natively - True Thrust becoming Raiden Thrust, Heat
                    // Blast becoming Blazing Shot - instead of being duplicated here.
                    return hook.Original(actionManager, suggestion.Action.Id);
                }
            }
        }
        catch (Exception ex)
        {
            // Never let a bug in the rotation take the player's hotbar with it.
            _log.Error(ex, "One Two Punch: resolve failed, passing the action through unchanged");
        }
        finally
        {
            _inDetour = false;
        }

        return hook.Original(actionManager, actionId);
    }

    public void Dispose()
    {
        try
        {
            _hook?.Disable();
            _hook?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "One Two Punch: hook disposal failed.");
        }

        _hook = null;
    }
}
