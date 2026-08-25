using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using OneTwoPunch.Core.Jobs;

namespace OneTwoPunch.Plugin.Services;

/// <summary>
/// Notices when the player actually uses an action, so the engine's weave budget and opener
/// stay in step with what was really pressed.
/// <para>
/// Two earlier attempts at this were wrong in the same way - they reported things that had
/// not happened, and the opener paid for it.
/// </para>
/// <para>
/// The first watched cooldowns: a recast that was zero last frame and is not zero now
/// looked like a use. But every global cooldown shares one recast timer, so casting a
/// single global made all of them jump at once and the whole job's action list was reported
/// used - a recorded pull showed twenty-six "casts" at one timestamp.
/// </para>
/// <para>
/// The second hooked UseAction, which is the function a keypress enters, not the one an
/// action leaves by. It is called with the id on the hotbar - our own button - before the
/// game resolves what that button currently is, and it returns true for a press that was
/// merely queued behind the running global. So a recorded pull showed "Doom Spike" for
/// every press on a Dragoon whose button was being replaced with the whole rotation, and
/// the opener aborted on the very first one: step one is True Thrust, the watcher said Doom
/// Spike, and the engine concluded the player had gone off script before the fight had
/// started. Neither of the two logs that found this ever ran an opener past step one.
/// </para>
/// <para>
/// So it hooks UseActionLocation, which is where both paths - pressed now, or dequeued a
/// moment later - converge, and which is called with the action the game has already
/// resolved. LastUsedActionSequence moving across the call is the game saying an action
/// really left the client; the return value alone only means the request was accepted. This
/// is what BossMod does, and it is the only signal here that means what it says.
/// </para>
/// <para>
/// The detour observes and never changes the outcome: the original is called first and its
/// answer returned untouched, so this cannot affect what the game does with a press.
/// </para>
/// </summary>
public sealed unsafe class ActionUseWatcher : IDisposable
{
    private readonly IPluginLog _log;
    private Hook<ActionManager.Delegates.UseActionLocation>? _hook;
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
                var address = ActionManager.Addresses.UseActionLocation.Value;
                if (address == 0)
                    return false;

                _hook = interop.HookFromAddress<ActionManager.Delegates.UseActionLocation>(address, Detour);
            }

            if (!_hook.IsEnabled)
                _hook.Enable();

            return true;
        }
        catch (Exception ex)
        {
            _unavailable = true;
            _log.Error(ex, "One Two Punch: could not hook UseActionLocation; opener and weave tracking are off.");
            return false;
        }
    }

    public void Disable() => _hook?.Disable();

    private bool Detour(
        ActionManager* self,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        Vector3* location,
        uint extraParam,
        byte mode)
    {
        var before = self->LastUsedActionSequence;

        var used = _hook!.Original(self, actionType, actionId, targetId, location, extraParam, mode);

        // The sequence number moving is the game saying this action went out. A press it
        // declined, or swallowed into the queue, leaves it where it was - and must not
        // advance the opener.
        if (actionType == ActionType.Action && self->LastUsedActionSequence != before)
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
