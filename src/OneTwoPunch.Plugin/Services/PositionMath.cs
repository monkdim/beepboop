using Dalamud.Game.ClientState.Objects.Types;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Plugin.Services;

/// <summary>
/// Where the player is standing relative to a target.
/// <para>
/// This only ever feeds the True North hint. If the facing convention is wrong the worst
/// case is a hint that does not fire - the rotation itself never depends on it, and the
/// whole thing can be switched off in the settings.
/// </para>
/// </summary>
public static class PositionMath
{
    public static RelativePosition Relative(IGameObject player, IGameObject target)
    {
        var dx = player.Position.X - target.Position.X;
        var dz = player.Position.Z - target.Position.Z;

        if ((dx * dx) + (dz * dz) < 0.0001f)
            return RelativePosition.Unknown;

        // Target rotation is the direction it faces, measured from -Z.
        var angleToPlayer = MathF.Atan2(dx, dz);
        var relative = Normalise(angleToPlayer - target.Rotation);
        var degrees = Math.Abs(relative * 180f / MathF.PI);

        return degrees switch
        {
            < 45f => RelativePosition.Front,
            > 135f => RelativePosition.Rear,
            _ => RelativePosition.Flank,
        };
    }

    private static float Normalise(float radians)
    {
        while (radians > MathF.PI)
            radians -= MathF.Tau;

        while (radians < -MathF.PI)
            radians += MathF.Tau;

        return radians;
    }
}
