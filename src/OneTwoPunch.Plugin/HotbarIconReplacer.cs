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
        var hook = _hook;
        if (hook is null)
            return 0;

        var original = hook.Original(slotType, actionId, costOffset, module, slot);

        if (_inDetour || slot is null)
            return original;

        _inDetour = true;
        try
        {
            // The assigned action, not the one the game just resolved: that is the id the
            // player put on the bar, and the only one our buttons are named by.
            if (slot->CommandType != RaptureHotbarModule.HotbarSlotType.Action)
                return original;

            var mode = _classify(slot->CommandId);
            if (mode is null)
                return original;

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
