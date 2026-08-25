using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using OneTwoPunch.Core.Jobs;

namespace OneTwoPunch.Plugin.Services;

/// <summary>
/// Notices when the player actually uses an action, so the engine's weave budget and opener
/// stay in step with what was really pressed.
/// <para>
/// This used to watch cooldowns instead of hooking: a recast that was zero last frame and
/// is not zero now looked like a use, and needed no signature. It was wrong, and badly.
/// Every global cooldown shares one recast timer, so casting a single global made all of
/// them jump at once and the watcher reported the entire job's action list as used - fifty
/// or more spurious uses per cast, each one advancing the opener and spending weave budget.
/// A recorded pull showed twenty-six "casts" at a single timestamp.
/// </para>
/// <para>
/// So it hooks UseAction, which is what BossMod does, through FFXIVClientStructs' own
/// resolved address and generated delegate. The detour observes and never changes the
/// outcome: the original is called first and its answer returned untouched, so this cannot
/// affect what the game does with a press.
/// </para>
/// </summary>
public sealed unsafe class ActionUseWatcher : IDisposable
{
    private readonly IPluginLog _log;
    private Hook<ActionManager.Delegates.UseAction>? _hook;
    private bool _unavailable;

    public ActionUseWatcher(IPluginLog log) => _log = log;

    public event Action<uint>? ActionUsed;

    /// <summary>Installs the hook. Safe to call repeatedly; returns false if unavailable.</summary>
    public bool Enable(IGameInteropProvider interop)
    {
        if (_unavailable)
            return false;

        try
        {
            if (_hook is null)
            {
                var address = ActionManager.Addresses.UseAction.Value;
                if (address == 0)
                    return false;

                _hook = interop.HookFromAddress<ActionManager.Delegates.UseAction>(address, Detour);
            }

            if (!_hook.IsEnabled)
                _hook.Enable();

            return true;
        }
        catch (Exception ex)
        {
            _unavailable = true;
            _log.Error(ex, "One Two Punch: could not hook UseAction; opener and weave tracking are off.");
            return false;
        }
    }

    public void Disable() => _hook?.Disable();

    private bool Detour(
        ActionManager* self,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        bool* outOptAreaTargeted)
    {
        var used = _hook!.Original(
            self, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);

        // Only a press the game actually accepted, and only a real action - not an item,
        // not a mount. A rejected press must not advance the opener.
        if (used && actionType == ActionType.Action)
        {
            try
            {
                ActionUsed?.Invoke(actionId);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "One Two Punch: handling a use failed");
            }
        }

        return used;
    }

    /// <summary>Kept for the job switch, which has no per-action state to forget any more.</summary>
    public void Track(IJobRotation job)
    {
    }

    public void Reset()
    {
    }

    public void Dispose()
    {
        _hook?.Disable();
        _hook?.Dispose();
        _hook = null;
    }
}
