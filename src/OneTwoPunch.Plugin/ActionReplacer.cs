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
    private delegate uint GetAdjustedActionIdDelegate(ActionManager* actionManager, uint actionId);

    private readonly IGameInteropProvider _interop;
    private readonly IPluginLog _log;

    private readonly Func<uint, RotationMode?> _classify;
    private readonly Func<RotationMode, Suggestion?> _resolve;

    private Hook<GetAdjustedActionIdDelegate>? _hook;

    /// <summary>Set when hooking has failed, so it is attempted once and not every frame.</summary>
    private bool _unavailable;

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
                // Hooked by member function pointer rather than a byte signature, so a patch
                // that shifts code around does not silently break the plugin. If the pointer
                // has not been resolved yet there is nothing to hook, and hooking address
                // zero would be catastrophic - so check before, not after.
                var address = (nint)ActionManager.MemberFunctionPointers.GetAdjustedActionId;
                if (address == 0)
                {
                    _log.Warning("One Two Punch: GetAdjustedActionId is not resolved yet; will retry.");
                    return false;
                }

                _hook = _interop.HookFromAddress<GetAdjustedActionIdDelegate>(address, Detour);
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

    private uint Detour(ActionManager* actionManager, uint actionId)
    {
        var hook = _hook;
        if (hook is null)
            return actionId;

        try
        {
            var mode = _classify(actionId);
            if (mode is not null)
            {
                var suggestion = _resolve(mode.Value);
                if (suggestion is not null && suggestion.Action.Id != 0)
                {
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
