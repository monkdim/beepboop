using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace TwoButton.Plugin.Services;

/// <summary>
/// Counts how many enemies the job's AoE would actually hit, so the AoE button can fall
/// back to single target on a lone boss instead of quietly losing damage.
/// </summary>
public static class EnemyCounter
{
    public static int CountAround(
        IObjectTable objects,
        IGameObject? target,
        float radius)
    {
        if (target is null)
            return 0;

        var count = 0;

        foreach (var obj in objects)
        {
            if (obj.ObjectKind != ObjectKind.BattleNpc)
                continue;

            if (obj is not IBattleNpc npc)
                continue;

            if (!npc.IsTargetable || npc.CurrentHp == 0)
                continue;

            // Only things actually fighting us. Otherwise a passive mob standing nearby
            // would flip the button to AoE mid-pull.
            if (npc.BattleNpcKind != BattleNpcSubKind.Enemy)
                continue;

            var dx = npc.Position.X - target.Position.X;
            var dz = npc.Position.Z - target.Position.Z;
            var reach = radius + npc.HitboxRadius;

            if ((dx * dx) + (dz * dz) <= reach * reach)
                count++;
        }

        return count;
    }
}
