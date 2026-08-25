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
/// </summary>
public sealed unsafe class ActionReplacer : IDisposable
{
    private delegate uint GetAdjustedActionIdDelegate(ActionManager* actionManager, uint actionId);

    private readonly Hook<GetAdjustedActionIdDelegate> _hook;
    private readonly IPluginLog _log;

    private readonly Func<uint, RotationMode?> _classify;
    private readonly Func<RotationMode, Suggestion?> _resolve;

    public ActionReplacer(
        IGameInteropProvider interop,
        IPluginLog log,
        Func<uint, RotationMode?> classify,
        Func<RotationMode, Suggestion?> resolve)
    {
        _log = log;
        _classify = classify;
        _resolve = resolve;

        // Hooked by member function pointer rather than a byte signature, so a patch that
        // shifts code around does not silently break the plugin.
        _hook = interop.HookFromAddress<GetAdjustedActionIdDelegate>(
            (nint)ActionManager.MemberFunctionPointers.GetAdjustedActionId,
            Detour);
    }

    public void Enable() => _hook.Enable();

    public void Disable() => _hook.Disable();

    private uint Detour(ActionManager* actionManager, uint actionId)
    {
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
                    return _hook.Original(actionManager, suggestion.Action.Id);
                }
            }
        }
        catch (Exception ex)
        {
            // Never let a bug in the rotation take the player's hotbar with it.
            _log.Error(ex, "One Two Punch: resolve failed, passing the action through unchanged");
        }

        return _hook.Original(actionManager, actionId);
    }

    public void Dispose()
    {
        _hook.Disable();
        _hook.Dispose();
    }
}
