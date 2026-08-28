using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Plugin.Services;

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
    /// <summary>How often the object table is swept to count enemies.</summary>
    private const double EnemyCountInterval = 0.2;

    private readonly List<StatusEntry> _self = [];
    private readonly List<StatusEntry> _target = [];

    private float _combatDuration;
    private bool _wasInCombat;

    // Enemy counting walks the whole object table. Even at once per frame that is a lot of
    // work for a number that changes on the timescale of a pull, not a frame.
    private int _enemyCount;
    private double _enemyCountedAt = double.NegativeInfinity;

    private double _rangeCheckedAt;
    private float _outOfRangeFor;

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
        s.Mp = player.CurrentMp;
        s.MaxMp = player.MaxMp;
        s.PlayerHpFraction = player.MaxHp > 0 ? player.CurrentHp / (float)player.MaxHp : 1f;
        s.InCombat = condition[ConditionFlag.InCombat];
        s.IsCasting = player.IsCasting;
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

        // Elapsed is zero when the recast is not running at all, which means the global is
        // ready now - not a full global away. Subtracting gave a whole GCD of imaginary
        // weave room at exactly the moment the answer should have been "press the global".
        s.GcdRemaining = elapsed <= 0f ? 0f : Math.Max(0f, total - elapsed);
        s.AnimationLock = Math.Max(0f, manager->AnimationLock);
    }

    private void FillTarget(CombatSnapshot s, IJobRotation job, IPlayerCharacter player)
    {
        var target = targets.Target;
        var battleTarget = target as IBattleChara;

        s.TargetId = battleTarget?.GameObjectId ?? CombatSnapshot.NoTarget;
        s.HasTarget = battleTarget is not null;
        s.TargetIsHostile = battleTarget is IBattleNpc;

        // Both addresses are handed straight to the game as pointers. A target that was
        // destroyed between frames leaves a live-looking wrapper around a null address, and
        // dereferencing that in native code takes the whole process down - so they are
        // checked here rather than trusted.
        var playerAddress = player.Address;
        var targetAddress = battleTarget?.Address ?? nint.Zero;

        s.TargetInRange = playerAddress != nint.Zero
            && targetAddress != nint.Zero
            && ActionManager.GetActionInRangeOrLoS(
                job.SingleTargetButton.Id,
                (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)playerAddress,
                (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)targetAddress) == 0;

        // Accumulated rather than read fresh, so the rules can ride out a blip. Reset the
        // moment reach comes back, so a genuine walk away still counts from when it started.
        var sinceLastCheck = _rangeCheckedAt > 0d ? (float)(s.Now - _rangeCheckedAt) : 0f;
        _rangeCheckedAt = s.Now;
        _outOfRangeFor = s.TargetInRange ? 0f : _outOfRangeFor + Math.Max(0f, sinceLastCheck);
        s.OutOfRangeFor = _outOfRangeFor;

        s.TargetHpFraction = battleTarget is { MaxHp: > 0 }
            ? (float)battleTarget.CurrentHp / battleTarget.MaxHp
            : 1f;

        // Nothing hostile and targetable means the boss is away: hold burst.
        s.InDowntime = battleTarget is null || !battleTarget.IsTargetable;

        if (s.Now - _enemyCountedAt >= EnemyCountInterval)
        {
            _enemyCount = EnemyCounter.CountAround(objects, battleTarget, job.AoeRadius);
            _enemyCountedAt = s.Now;
        }

        s.EnemiesInAoeRange = _enemyCount;

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

    /// <summary>
    /// Maps the live job gauge onto the engine's plain structs. Member names here were read
    /// off the Dalamud build in CI rather than guessed - see the "Probe Dalamud API surface"
    /// step in .github/workflows/ci.yml.
    /// </summary>
    private void FillGauges(CombatSnapshot s, IJobRotation job)
    {
        switch (job.JobId)
        {
            case 19:
            {
                s.Gauges.Paladin.Oath = gauges.Get<PLDGauge>().OathGauge;
                break;
            }

            case 21:
            {
                s.Gauges.Warrior.BeastGauge = gauges.Get<WARGauge>().BeastGauge;
                break;
            }

            case 32:
            {
                var g = gauges.Get<DRKGauge>();
                ref var t = ref s.Gauges.DarkKnight;
                t.Blood = g.Blood;
                t.DarksideTimeRemaining = g.DarksideTimeRemaining / 1000f;
                t.ShadowTimeRemaining = g.ShadowTimeRemaining / 1000f;
                t.HasDarkArts = g.HasDarkArts;
                t.DeliriumStep = (byte)g.DeliriumComboStep;
                break;
            }

            case 37:
            {
                var g = gauges.Get<GNBGauge>();
                ref var t = ref s.Gauges.Gunbreaker;
                t.Ammo = g.Ammo;
                t.AmmoComboStep = g.AmmoComboStep;
                break;
            }

            case 20:
            {
                var g = gauges.Get<MNKGauge>();
                ref var t = ref s.Gauges.Monk;
                t.Chakra = g.Chakra;
                t.BlitzTimeRemaining = g.BlitzTimeRemaining / 1000f;
                t.OpoOpoFury = g.OpoOpoFury;
                t.RaptorFury = g.RaptorFury;
                t.CoeurlFury = g.CoeurlFury;
                t.NadiFlags = (byte)g.Nadi;

                // Compared as raw values so the engine needs no Dalamud enum.
                var opened = 0;
                var first = 0;
                var matching = true;
                foreach (var chakra in g.BeastChakra)
                {
                    var value = (int)chakra;
                    if (value == 0)
                        continue;

                    if (opened == 0)
                        first = value;
                    else if (value != first)
                        matching = false;

                    opened++;
                }

                t.BeastChakraCount = (byte)opened;
                t.BeastChakraMatching = opened > 0 && matching;
                break;
            }

            case 22:
            {
                var g = gauges.Get<DRGGauge>();
                s.Gauges.Dragoon.FirstmindsFocus = g.FirstmindsFocusCount;
                s.Gauges.Dragoon.LotdTimeRemaining = g.LOTDTimer / 1000f;
                break;
            }

            case 23:
            {
                var g = gauges.Get<BRDGauge>();
                ref var t = ref s.Gauges.Bard;
                t.SongTimeRemaining = g.SongTimer / 1000f;
                t.Repertoire = g.Repertoire;
                t.SoulVoice = g.SoulVoice;
                t.SongId = (byte)g.Song;

                var coda = 0;
                foreach (var song in g.Coda)
                {
                    if ((int)song != 0)
                        coda++;
                }

                t.CodaCount = (byte)coda;
                break;
            }

            case 25:
            {
                var g = gauges.Get<BLMGauge>();
                ref var t = ref s.Gauges.BlackMage;
                t.AstralFire = g.AstralFireStacks;
                t.UmbralIce = g.UmbralIceStacks;
                t.UmbralHearts = g.UmbralHearts;
                t.PolyglotStacks = g.PolyglotStacks;
                t.AstralSoulStacks = (byte)Math.Clamp(g.AstralSoulStacks, 0, 255);
                t.EnochianTimeRemaining = g.EnochianTimer / 1000f;
                t.ParadoxActive = g.IsParadoxActive;
                break;
            }

            case 27:
            {
                var g = gauges.Get<SMNGauge>();
                ref var t = ref s.Gauges.Summoner;
                t.AetherflowStacks = g.AetherflowStacks;
                t.Attunement = g.Attunement;
                t.SummonTimeRemaining = g.SummonTimerRemaining / 1000f;
                t.IfritReady = g.IsIfritReady;
                t.TitanReady = g.IsTitanReady;
                t.GarudaReady = g.IsGarudaReady;
                t.BahamutReady = g.IsBahamutReady;
                t.PhoenixReady = g.IsPhoenixReady;
                t.IfritAttuned = g.IsIfritAttuned;
                t.TitanAttuned = g.IsTitanAttuned;
                t.GarudaAttuned = g.IsGarudaAttuned;
                break;
            }

            case 30:
            {
                var g = gauges.Get<NINGauge>();
                s.Gauges.Ninja.Ninki = g.Ninki;
                s.Gauges.Ninja.Kazematoi = g.Kazematoi;
                break;
            }

            case 31:
            {
                var g = gauges.Get<MCHGauge>();
                ref var t = ref s.Gauges.Machinist;
                t.Heat = g.Heat;
                t.Battery = g.Battery;
                t.LastSummonBatteryPower = g.LastSummonBatteryPower;
                t.Overheated = g.IsOverheated;
                t.OverheatTimeRemaining = g.OverheatTimeRemaining / 1000f;
                t.RobotActive = g.IsRobotActive;
                t.SummonTimeRemaining = g.SummonTimeRemaining / 1000f;
                break;
            }

            case 34:
            {
                var g = gauges.Get<SAMGauge>();
                ref var t = ref s.Gauges.Samurai;
                t.Kenki = g.Kenki;
                t.Meditation = g.MeditationStacks;
                t.HasSetsu = g.HasSetsu;
                t.HasGetsu = g.HasGetsu;
                t.HasKa = g.HasKa;
                break;
            }

            case 35:
            {
                var g = gauges.Get<RDMGauge>();
                s.Gauges.RedMage.WhiteMana = g.WhiteMana;
                s.Gauges.RedMage.BlackMana = g.BlackMana;
                s.Gauges.RedMage.ManaStacks = g.ManaStacks;
                break;
            }

            case 38:
            {
                var g = gauges.Get<DNCGauge>();
                ref var t = ref s.Gauges.Dancer;
                t.Feathers = g.Feathers;
                t.Esprit = g.Esprit;
                t.CompletedSteps = g.CompletedSteps;
                t.Dancing = g.IsDancing;
                t.NextStep = g.NextStep;
                break;
            }

            case 39:
            {
                var g = gauges.Get<RPRGauge>();
                ref var t = ref s.Gauges.Reaper;
                t.Soul = g.Soul;
                t.Shroud = g.Shroud;
                t.EnshroudTimeRemaining = g.EnshroudedTimeRemaining / 1000f;
                t.LemureShroud = g.LemureShroud;
                t.VoidShroud = g.VoidShroud;
                break;
            }

            case 41:
            {
                var g = gauges.Get<VPRGauge>();
                ref var t = ref s.Gauges.Viper;
                t.RattlingCoils = g.RattlingCoilStacks;
                t.SerpentOffering = g.SerpentOffering;
                t.AnguineTribute = g.AnguineTribute;
                t.DreadCombo = (DreadCombo)(byte)g.DreadCombo;
                t.SerpentCombo = (SerpentCombo)(byte)g.SerpentCombo;
                break;
            }

            case 42:
            {
                var g = gauges.Get<PCTGauge>();
                ref var t = ref s.Gauges.Pictomancer;

                // Dalamud spells this member "PalleteGauge".
                t.PaletteGauge = g.PalleteGauge;
                t.Paint = g.Paint;
                t.CreatureMotifDrawn = g.CreatureMotifDrawn;
                t.WeaponMotifDrawn = g.WeaponMotifDrawn;
                t.LandscapeMotifDrawn = g.LandscapeMotifDrawn;
                t.MooglePortraitReady = g.MooglePortraitReady;
                t.MadeenPortraitReady = g.MadeenPortraitReady;
                break;
            }
        }
    }
}
