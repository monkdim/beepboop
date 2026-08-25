using System.Numerics;
using Dalamud.Plugin.Services;

namespace TwoButton.Plugin.Services;

/// <summary>
/// Works out whether the player is moving, by watching their position between frames.
/// <para>
/// The raw answer flickers - a single stationary frame mid-strafe would make a caster's
/// button jump to an instant and back. So movement is latched: it turns on immediately (a
/// suggestion that assumes you are standing still is the one that costs you a cast) and
/// off only after a short settle.
/// </para>
/// </summary>
public sealed class MovementTracker
{
    private const float MovementThreshold = 0.01f;
    private const float SettleSeconds = 0.2f;

    private Vector3 _lastPosition;
    private bool _hasPosition;
    private float _sinceLastMovement;

    public bool IsMoving { get; private set; }

    public float MovingFor { get; private set; }

    public float StillFor { get; private set; }

    public void Reset()
    {
        _hasPosition = false;
        IsMoving = false;
        MovingFor = 0f;
        StillFor = 0f;
        _sinceLastMovement = 0f;
    }

    public void Update(IClientState clientState, float deltaSeconds)
    {
        var player = clientState.LocalPlayer;
        if (player is null)
        {
            Reset();
            return;
        }

        var position = player.Position;

        if (!_hasPosition)
        {
            _lastPosition = position;
            _hasPosition = true;
            return;
        }

        var moved = Vector3.DistanceSquared(position, _lastPosition) > MovementThreshold * MovementThreshold;
        _lastPosition = position;

        if (moved)
        {
            _sinceLastMovement = 0f;
            if (!IsMoving)
            {
                IsMoving = true;
                MovingFor = 0f;
            }

            MovingFor += deltaSeconds;
            StillFor = 0f;
            return;
        }

        _sinceLastMovement += deltaSeconds;

        if (IsMoving && _sinceLastMovement < SettleSeconds)
        {
            MovingFor += deltaSeconds;
            return;
        }

        IsMoving = false;
        MovingFor = 0f;
        StillFor += deltaSeconds;
    }
}
