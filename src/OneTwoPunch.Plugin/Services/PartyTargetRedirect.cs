using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace OneTwoPunch.Plugin.Services;

/// <summary>
/// Sends Aetherial Manipulation to the tank when nothing worth jumping to is targeted.
/// <para>
/// Aetherial Manipulation teleports the Black Mage to a party member, so it needs one
/// targeted - and in the moment you want it, what is targeted is the boss. The usual answer
/// is a macro with a placeholder, which costs the player a second hotbar slot and a second
/// key. This does the same job on the key they already have.
/// </para>
/// <para>
/// It only ever edits a press the player made. The hook sits on UseAction, the function a
/// keypress enters, and changes one argument - who it is aimed at - before handing it
/// straight back to the game. Nothing is sent on anybody's behalf, no press is generated,
/// and a press with a party member already targeted is passed through untouched. That is
/// the same bargain as the icon: answer the question the game asked, do not ask one.
/// </para>
/// </summary>
public sealed unsafe class PartyTargetRedirect : IDisposable
{
    /// <summary>Aetherial Manipulation. The only action this touches.</summary>
    private const uint AetherialManipulation = 155;

    /// <summary>The game's own "no target" sentinel.</summary>
    private const ulong NoTarget = 0xE000_0000;

    /// <summary>ClassJob.Role for a tank, from the game's own sheet.</summary>
    private const byte TankRole = 1;

    private readonly IGameInteropProvider _interop;
    private readonly IPluginLog _log;
    private readonly IPartyList _party;
    private readonly Func<bool> _enabled;

    private Hook<ActionManager.Delegates.UseAction>? _hook;
    private bool _unavailable;

    private long _redirected;

    public PartyTargetRedirect(
        IGameInteropProvider interop,
        IPluginLog log,
        IPartyList party,
        Func<bool> enabled)
    {
        _interop = interop;
        _log = log;
        _party = party;
        _enabled = enabled;
    }

    public bool IsActive => _hook is { IsEnabled: true };

    /// <summary>How many presses have been pointed at a tank.</summary>
    public long TimesRedirected => _redirected;

    public bool Enable()
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

                _hook = _interop.HookFromAddress<ActionManager.Delegates.UseAction>(address, Detour);
                _log.Information("One Two Punch: hooked UseAction for party targeting at 0x{Address:X}.", address);
            }

            if (!_hook.IsEnabled)
                _hook.Enable();

            return true;
        }
        catch (Exception ex)
        {
            // Never worth the game. Without it the key behaves exactly as the game intends.
            _unavailable = true;
            _log.Error(ex, "One Two Punch: could not hook UseAction; Aetherial Manipulation will need its own target.");
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
            _log.Error(ex, "One Two Punch: could not disable the party targeting hook.");
        }
    }

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
        try
        {
            if (_enabled()
                && actionType == ActionType.Action
                && actionId == AetherialManipulation
                && !IsPartyMember(targetId))
            {
                var tank = FindTank();
                if (tank != 0)
                {
                    _redirected++;
                    targetId = tank;
                }
            }
        }
        catch (Exception ex)
        {
            // Fall through with the target the player actually had.
            _log.Error(ex, "One Two Punch: party targeting failed, leaving the press alone");
        }

        return _hook!.Original(self, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
    }

    /// <summary>Whether the press already has somebody worth jumping to.</summary>
    private bool IsPartyMember(ulong targetId)
    {
        if (targetId is 0 or NoTarget)
            return false;

        for (var i = 0; i < _party.Length; i++)
        {
            if (_party[i]?.GameObject?.GameObjectId == targetId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The first tank in the party list, which is the party's own ordering rather than ours -
    /// in a full party that is the main tank. Zero when there is no party, or no tank in it,
    /// in which case the press is left exactly as the player made it.
    /// </summary>
    private ulong FindTank()
    {
        for (var i = 0; i < _party.Length; i++)
        {
            var member = _party[i];
            if (member?.GameObject is null)
                continue;

            if (member.ClassJob.Value.Role != TankRole)
                continue;

            return member.GameObject.GameObjectId;
        }

        return 0;
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
            _log.Error(ex, "One Two Punch: party targeting hook disposal failed.");
        }

        _hook = null;
    }
}
