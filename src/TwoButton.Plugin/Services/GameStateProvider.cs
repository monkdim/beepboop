using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using TwoButton.Core.Jobs;
using TwoButton.Core.Model;

namespace TwoButton.Plugin.Services;

/// <summary>
/// Turns the live game into a <see cref="CombatSnapshot"/>. This is the only place in the
/// plugin that knows about both Dalamud and the rotation engine, which is what keeps the
/// engine testable without the game.
/// </summary>
public sealed unsafe class GameStateProvider(
    ICondition condition,
    ITargetManager targets,
    IObjectTable objects,
    IJobGauges gauges,
    MovementTracker movement,
    PotionTracker potions,
    Configuration config)
{
    private readonly CombatSnapshot _snapshot = new();
    private readonly List<StatusEntry> _self = [];
    private readonly List<StatusEntry> _target = [];

    private float _combatDuration;
    private bool _wasInCombat;

    public void Tick(float deltaSeconds)
    {
        var inCombat = condition[ConditionFlag.InCombat];

        if (inCombat && !_wasInCombat)
            _combatDuration = 0f;
        else if (inCombat)
            _combatDuration += deltaSeconds;
        else
            _combatDuration = 0f;

        _wasInCombat = inCombat;
    }

    public CombatSnapshot? Build(IJobRotation job, double now)
    {
        var player = objects.LocalPlayer;
        if (player is null)
            return null;

        var s = _snapshot;
        s.JobId = player.ClassJob.RowId;
        s.Level = player.Level;
        s.InCombat = condition[ConditionFlag.InCombat];
        s.CombatDuration = _combatDuration;
        s.Now = now;

        s.IsMoving = movement.IsMoving;
        s.MovingFor = movement.MovingFor;
        s.StillFor = movement.StillFor;

        FillGcd(s, job);
        FillTarget(s, job, player);
        FillStatuses(s, player);
        FillCombo(s);
        FillGauges(s, job);
        FillPotion(s);

        return s;
    }

    private void FillGcd(CombatSnapshot s, IJobRotation job)
    {
        var manager = ActionManager.Instance();
        if (manager is null)
            return;

        // Read the GCD off the job's own basic weaponskill, so it reflects the player's
        // actual skill/spell speed rather than an assumed 2.5s.
        var probe = job.SingleTargetButton.Id;
        var total = manager->GetRecastTime(ActionType.Action, probe);
        var elapsed = manager->GetRecastTimeElapsed(ActionType.Action, probe);

        s.GcdTotal = total > 0f ? total : 2.5f;
        s.GcdRemaining = Math.Max(0f, total - elapsed);
        s.AnimationLock = Math.Max(0f, manager->AnimationLock);
    }

    private void FillTarget(CombatSnapshot s, IJobRotation job, IPlayerCharacter player)
    {
        var target = targets.Target;
        var battleTarget = target as IBattleChara;

        s.HasTarget = battleTarget is not null;
        s.TargetIsHostile = battleTarget is IBattleNpc;
        s.TargetInRange = battleTarget is not null
            && ActionManager.GetActionInRangeOrLoS(
                job.SingleTargetButton.Id,
                (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)player.Address,
                (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)battleTarget.Address) == 0;

        s.TargetHpFraction = battleTarget is { MaxHp: > 0 }
            ? (float)battleTarget.CurrentHp / battleTarget.MaxHp
            : 1f;

        // Nothing hostile and targetable means the boss is away: hold burst.
        s.InDowntime = battleTarget is null || !battleTarget.IsTargetable;

        s.EnemiesInAoeRange = EnemyCounter.CountAround(objects, battleTarget, job.AoeRadius);

        s.Position = config.DetectPositionals && battleTarget is not null
            ? PositionMath.Relative(player, battleTarget)
            : RelativePosition.Unknown;
    }

    private void FillPotion(CombatSnapshot s)
    {
        if (!config.PotionEnabled || config.PotionItemId == 0)
        {
            s.PotionAvailable = false;
            s.PotionCooldownRemaining = float.MaxValue;
            return;
        }

        var remaining = potions.CooldownRemaining(config.PotionItemId, config.PotionPreferHq);
        s.PotionCooldownRemaining = remaining;
        s.PotionAvailable = remaining <= 0f;
    }

    private void FillStatuses(CombatSnapshot s, IPlayerCharacter player)
    {
        _self.Clear();
        foreach (var status in player.StatusList)
        {
            if (status.StatusId == 0)
                continue;

            _self.Add(new StatusEntry(
                status.StatusId,
                status.RemainingTime < 0f ? float.PositiveInfinity : status.RemainingTime,
                (byte)status.Param));
        }

        _target.Clear();
        if (targets.Target is IBattleChara battleTarget)
        {
            foreach (var status in battleTarget.StatusList)
            {
                if (status.StatusId == 0)
                    continue;

                // Only our own debuffs. Another Dragoon's dot must never suppress ours.
                if (status.SourceId != player.EntityId)
                    continue;

                _target.Add(new StatusEntry(
                    status.StatusId,
                    status.RemainingTime < 0f ? float.PositiveInfinity : status.RemainingTime,
                    (byte)status.Param));
            }
        }

        s.SelfStatuses = _self;
        s.TargetStatuses = _target;
    }

    private static void FillCombo(CombatSnapshot s)
    {
        var manager = ActionManager.Instance();
        if (manager is null)
            return;

        s.ComboTimeRemaining = Math.Max(0f, manager->Combo.Timer);
        s.LastComboAction = s.ComboTimeRemaining > 0f ? manager->Combo.Action : 0u;
    }

    private void FillGauges(CombatSnapshot s, IJobRotation job)
    {
        switch (job.JobId)
        {
            case 22:
            {
                var g = gauges.Get<DRGGauge>();
                s.Gauges.Dragoon.FirstmindsFocus = g.FirstmindsFocusCount;
                s.Gauges.Dragoon.LotdTimeRemaining = g.LOTDTimer / 1000f;
                break;
            }

            case 31:
            {
                var g = gauges.Get<MCHGauge>();
                s.Gauges.Machinist.Heat = g.Heat;
                s.Gauges.Machinist.Battery = g.Battery;
                s.Gauges.Machinist.LastSummonBatteryPower = g.LastSummonBatteryPower;
                s.Gauges.Machinist.Overheated = g.IsOverheated;
                s.Gauges.Machinist.OverheatTimeRemaining = g.OverheatTimeRemaining / 1000f;
                s.Gauges.Machinist.RobotActive = g.IsRobotActive;
                s.Gauges.Machinist.SummonTimeRemaining = g.SummonTimeRemaining / 1000f;
                break;
            }
        }
    }
}
