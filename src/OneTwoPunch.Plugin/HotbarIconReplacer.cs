using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Plugin;

/// <summary>
/// The other half of the trick, and the half that was missing for four builds.
/// <para>
/// Answering <c>GetAdjustedActionId</c> is what makes the key fire the right ability, and it
/// worked from the start - a recorded pull has 99 of 101 presses matching the suggestion. It
/// does not touch the icon. The game resolves what a slot should *draw* somewhere else
/// entirely, in <c>RaptureHotbarModule.GetSlotAppearance</c>, which its own documentation
/// describes as running "every frame for every visible hotbar slot" and resolving "adjusted
/// action IDs as appropriate". That resolution never reached our hook, so the slot kept
/// drawing the action assigned to it.
/// </para>
/// <para>
/// This was invisible until the hotbar was asked directly. The hook counters said the game
/// was asking and being answered, which was true and beside the point; the slot's own
/// <c>ApparentActionId</c> - the field <c>IconId</c> is loaded from - still read Slice while
/// the last answer handed back was Soul Slice. Asking us and drawing our answer turned out
/// to be two different questions.
/// </para>
/// <para>
/// Still nothing but an answer to a question the game asked. No input is sent, and the slot
/// is not written to - only the appearance the game is in the middle of computing.
/// </para>
/// </summary>
public sealed unsafe class HotbarIconReplacer : IDisposable
{
    private readonly IGameInteropProvider _interop;
    private readonly IPluginLog _log;

    private readonly Func<uint, RotationMode?> _classify;
    private readonly Func<RotationMode, Suggestion?> _resolve;

    private Hook<RaptureHotbarModule.Delegates.GetSlotAppearance>? _hook;

    private bool _unavailable;
    private bool _warnedUnresolved;

    private long _drawn;
    private long _replaced;

    /// <summary>
    /// Every entry into the detour, counted before anything can turn it away.
    /// <para>
    /// The counter below it used to be the first thing in the method that ran, but it sits
    /// behind two guards - a re-entry test and a null slot - and a recorded pull came back
    /// reading "the hook saw 0 slots drawn" with the hook reporting itself installed. Zero
    /// there means one of three things and the number could not say which: the detour never
    /// ran at all, it ran and turned itself away, or it ran and the game handed it no slot.
    /// So the count of entries is taken first, and the reasons are counted beside it.
    /// </para>
    /// </summary>
    private long _calls;

    /// <summary>Entries where the game passed no slot to draw.</summary>
    private long _nullSlots;

    /// <summary>Entries turned away because this thread was already inside the detour.</summary>
    private long _reentrant;

    /// <summary>Where the hook was installed, or 0 if it never was.</summary>
    private nint _address;

    /// <summary>Every slot the game drew while the hook was live, ours or not.</summary>
    private long _slots;

    /// <summary>Slots that were not actions at all - a macro, an item, an emote.</summary>
    private long _notActions;

    /// <summary>
    /// A few of the action ids the game asked us to draw and we did not recognise.
    /// <para>
    /// This exists because a recorded pull came back reading "a slot holding one of your
    /// buttons was drawn 0 times" with the action hook answering four thousand times in the
    /// same fight. Both halves ask the same question of the same dictionary, so the two can
    /// only disagree about which id they are asking about - and that is a question no amount
    /// of reading the code was going to settle.
    /// </para>
    /// </summary>
    private readonly HashSet<uint> _unrecognised = [];

    private const int UnrecognisedSampleSize = 12;

    public HotbarIconReplacer(
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

    public bool IsActive => _hook is { IsEnabled: true };

    /// <summary>How many times a slot holding one of our buttons has been drawn.</summary>
    public long TimesDrawn => _drawn;

    /// <summary>How many of those were given our suggestion to draw instead.</summary>
    public long TimesReplaced => _replaced;

    /// <summary>Every slot drawn, ours or not - so "none of them were ours" can be told
    /// apart from "the hook never ran".</summary>
    public long SlotsSeen => _slots;

    /// <summary>Every entry into the detour, before any of the guards.</summary>
    public long TimesEntered => _calls;

    /// <summary>Of those, the ones the game handed no slot.</summary>
    public long EntriesWithNoSlot => _nullSlots;

    /// <summary>Of those, the ones turned away as re-entrant.</summary>
    public long ReentrantEntries => _reentrant;

    /// <summary>The address the hook was installed at, or 0.</summary>
    public nint Address => _address;

    /// <summary>Of those, the ones that were not actions.</summary>
    public long SlotsThatWereNotActions => _notActions;

    /// <summary>A sample of the action ids drawn that were not one of our buttons.</summary>
    public IReadOnlyCollection<uint> UnrecognisedIds => _unrecognised;

    /// <summary>
    /// True while this thread is inside the detour, for the same reason the action hook needs
    /// it: working out a suggestion asks the game questions, and a question that came back
    /// through here would recurse.
    /// </summary>
    [ThreadStatic]
    private static bool _inDetour;

    public bool Enable()
    {
        if (_unavailable)
            return false;

        try
        {
            if (_hook is null)
            {
                var address = RaptureHotbarModule.Addresses.GetSlotAppearance.Value;
                if (address == 0)
                {
                    if (!_warnedUnresolved)
                    {
                        _warnedUnresolved = true;
                        _log.Warning("One Two Punch: GetSlotAppearance is not resolved yet; icons will not follow.");
                    }

                    return false;
                }

                _hook = _interop.HookFromAddress<RaptureHotbarModule.Delegates.GetSlotAppearance>(address, Detour);
                _address = address;
                _log.Information("One Two Punch: hooked GetSlotAppearance at 0x{Address:X}.", address);
            }

            if (!_hook.IsEnabled)
                _hook.Enable();

            return true;
        }
        catch (Exception ex)
        {
            // The icon is worth having, but not at the cost of the game. If it cannot be
            // hooked the plugin still works; the slot just keeps its own art.
            _unavailable = true;
            _log.Error(ex, "One Two Punch: could not hook GetSlotAppearance; icons will not follow the suggestion.");
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
            _log.Error(ex, "One Two Punch: could not disable the icon hook.");
        }
    }

    private uint Detour(
        RaptureHotbarModule.HotbarSlotType* slotType,
        uint* actionId,
        ushort* costOffset,
        RaptureHotbarModule* module,
        RaptureHotbarModule.HotbarSlot* slot)
    {
        // First, before any guard can turn this away. See the field for why.
        _calls++;

        var hook = _hook;
        if (hook is null)
            return 0;

        var original = hook.Original(slotType, actionId, costOffset, module, slot);

        if (_inDetour)
        {
            _reentrant++;
            return original;
        }

        if (slot is null)
        {
            _nullSlots++;
            return original;
        }

        _inDetour = true;
        try
        {
            _slots++;

            if (slot->CommandType != RaptureHotbarModule.HotbarSlotType.Action)
            {
                _notActions++;
                return original;
            }

            // Three ids, because one was not enough and the log could not say which.
            //
            // CommandId is what the player put on the bar. OriginalApparentActionId is the
            // game's own "base action for display", which it keeps precisely so an upgraded
            // action still knows what it started as. And the resolved id is what the game
            // has just decided to draw, which is the right answer whenever the slot carries
            // something that resolves into one of our buttons rather than being one.
            //
            // Any of the three matching means this slot is ours. They can only match on a
            // slot the player put one of our two actions on, so a wider net here cannot
            // catch somebody else's key.
            var mode = _classify(slot->CommandId)
                       ?? _classify(slot->OriginalApparentActionId)
                       ?? _classify(*actionId);

            if (mode is null)
            {
                if (_unrecognised.Count < UnrecognisedSampleSize)
                    _unrecognised.Add(slot->CommandId);

                return original;
            }

            _drawn++;

            var suggestion = _resolve(mode.Value);
            if (suggestion is null || suggestion.Action.Id == 0)
                return original;

            // Written into the appearance the game is in the middle of computing, which it
            // then stores as the slot's ApparentActionId and loads the icon from.
            *slotType = RaptureHotbarModule.HotbarSlotType.Action;
            *actionId = suggestion.Action.Id;
            _replaced++;

            return suggestion.Action.Id;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "One Two Punch: icon resolve failed, leaving the slot alone");
            return original;
        }
        finally
        {
            _inDetour = false;
        }
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
            _log.Error(ex, "One Two Punch: icon hook disposal failed.");
        }

        _hook = null;
    }
}
