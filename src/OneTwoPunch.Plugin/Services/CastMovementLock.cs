using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

namespace OneTwoPunch.Plugin.Services;

/// <summary>
/// Holds the player still through a cast, and lets go in time to slidecast.
/// <para>
/// A cast that is interrupted by a step is a global thrown away, and stepping is not always
/// a decision - a knocked hand, a stick that does not centre, a spasm. That is the whole
/// reason this plugin exists, and it is the one thing a suggestion cannot help with: by the
/// time the button says Fire IV, the cast has already been cancelled.
/// </para>
/// <para>
/// The last half second or so of a cast is already committed - the spell goes off whatever
/// you do - so the lock lifts there rather than at the end. That is slidecasting, and it is
/// the difference between a lock that costs uptime and one that hands it back.
/// </para>
/// <para>
/// This is the only thing here that takes something away from the player rather than
/// answering a question, and it is dangerous in a way nothing else is: held still at the
/// wrong moment is a death. So it is off unless asked for, hooked only once asked for, and
/// it never holds anyone through anything but their own cast.
/// </para>
/// </summary>
public sealed unsafe class CastMovementLock : IDisposable
{
    /// <summary>
    /// The walk-input function, taken from BossMod's movement override rather than recalled -
    /// a signature that resolves to the wrong instruction writes floats through pointers into
    /// somebody else's arguments, which is a crash rather than a bad suggestion.
    /// </summary>
    private const string RmiWalkSignature = "E8 ?? ?? ?? ?? 80 7B 3E 00 48 8D 3D";

    /// <summary>
    /// Sums the movement input for the frame. Only the first two out-params matter here:
    /// zeroing them is the whole of the lock, and turning is deliberately left alone so the
    /// camera and facing still work while held.
    /// </summary>
    private delegate void RmiWalk(
        void* self,
        float* sumLeft,
        float* sumForward,
        float* sumTurnLeft,
        byte* haveBackwardOrStrafe,
        byte* a6,
        byte additive);

    private readonly IGameInteropProvider _interop;
    private readonly IPluginLog _log;
    private readonly Func<bool> _enabled;
    private readonly Func<float> _slidecastWindow;
    private readonly Func<IPlayerCharacter?> _player;

    private Dalamud.Hooking.Hook<RmiWalk>? _hook;
    private bool _unavailable;

    private bool _holding;

    public CastMovementLock(
        IGameInteropProvider interop,
        IPluginLog log,
        Func<bool> enabled,
        Func<float> slidecastWindow,
        Func<IPlayerCharacter?> player)
    {
        _interop = interop;
        _log = log;
        _enabled = enabled;
        _slidecastWindow = slidecastWindow;
        _player = player;
    }

    public bool IsActive => _hook is { IsEnabled: true };

    /// <summary>True while the player is actually being held. For the status line.</summary>
    public bool Holding => _holding;

    public bool Enable()
    {
        if (_unavailable)
            return false;

        try
        {
            _hook ??= _interop.HookFromSignature<RmiWalk>(RmiWalkSignature, Detour);

            if (!_hook.IsEnabled)
                _hook.Enable();

            _log.Information("One Two Punch: movement lock armed.");
            return true;
        }
        catch (Exception ex)
        {
            // A signature that will not resolve is the safe failure, and the only acceptable
            // one: without the hook the player simply moves as they always did.
            _unavailable = true;
            _holding = false;
            _log.Error(ex, "One Two Punch: could not hook movement; the cast lock is off.");
            return false;
        }
    }

    public void Disable()
    {
        _holding = false;

        try
        {
            if (_hook is { IsEnabled: true })
                _hook.Disable();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "One Two Punch: could not disable the movement lock.");
        }
    }

    private void Detour(
        void* self,
        float* sumLeft,
        float* sumForward,
        float* sumTurnLeft,
        byte* haveBackwardOrStrafe,
        byte* a6,
        byte additive)
    {
        _hook!.Original(self, sumLeft, sumForward, sumTurnLeft, haveBackwardOrStrafe, a6, additive);

        try
        {
            _holding = ShouldHold();
            if (_holding)
            {
                *sumLeft = 0f;
                *sumForward = 0f;
            }
        }
        catch (Exception ex)
        {
            // Never leave somebody rooted because a check threw.
            _holding = false;
            _log.Error(ex, "One Two Punch: movement lock check failed, letting go");
        }
    }

    /// <summary>
    /// Whether this frame is inside a cast the player has not yet committed. Everything is a
    /// reason to let go: the feature being off, no player, not casting, an instant, and the
    /// slidecast window itself.
    /// </summary>
    private bool ShouldHold()
    {
        if (!_enabled())
            return false;

        var player = _player();
        if (player is null || !player.IsCasting)
            return false;

        var total = player.TotalCastTime;
        if (total <= 0f)
            return false;

        var remaining = total - player.CurrentCastTime;
        return remaining > _slidecastWindow();
    }

    public void Dispose()
    {
        _holding = false;

        try
        {
            _hook?.Disable();
            _hook?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "One Two Punch: movement lock disposal failed.");
        }

        _hook = null;
    }
}
