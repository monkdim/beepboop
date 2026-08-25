using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Tests;

/// <summary>Fluent builder for a plausible mid-fight snapshot.</summary>
public sealed class SnapshotBuilder
{
    private readonly CombatSnapshot _snapshot = new()
    {
        Level = 100,
        InCombat = true,
        CombatDuration = 60f,
        GcdTotal = 2.5f,
        GcdRemaining = 0f,
        AnimationLock = 0f,
        HasTarget = true,
        TargetIsHostile = true,
        TargetInRange = true,
        TargetHpFraction = 1f,
        EnemiesInAoeRange = 1,
        Position = RelativePosition.Rear,
        Mp = 10000,
        MaxMp = 10000,
    };

    private readonly List<StatusEntry> _self = [];
    private readonly List<StatusEntry> _target = [];

    public SnapshotBuilder Job(uint jobId)
    {
        _snapshot.JobId = jobId;
        return this;
    }

    /// <summary>Current mana. Black Mage's phase choice turns on it.</summary>
    public SnapshotBuilder Mp(uint mp)
    {
        _snapshot.Mp = mp;
        return this;
    }

    public SnapshotBuilder Level(byte level)
    {
        _snapshot.Level = level;
        return this;
    }

    public SnapshotBuilder Gcd(float remaining, float total = 2.5f)
    {
        _snapshot.GcdRemaining = remaining;
        _snapshot.GcdTotal = total;
        return this;
    }

    public SnapshotBuilder AnimationLock(float seconds)
    {
        _snapshot.AnimationLock = seconds;
        return this;
    }

    public SnapshotBuilder Combo(uint lastAction, float timeLeft = 20f)
    {
        _snapshot.LastComboAction = lastAction;
        _snapshot.ComboTimeRemaining = timeLeft;
        return this;
    }

    public SnapshotBuilder NoCombo()
    {
        _snapshot.LastComboAction = 0;
        _snapshot.ComboTimeRemaining = 0f;
        return this;
    }

    public SnapshotBuilder Enemies(int count)
    {
        _snapshot.EnemiesInAoeRange = count;
        return this;
    }

    public SnapshotBuilder Buff(uint statusId, float remaining = 20f, byte stacks = 1)
    {
        _self.Add(new StatusEntry(statusId, remaining, stacks));
        return this;
    }

    public SnapshotBuilder Debuff(uint statusId, float remaining = 20f, byte stacks = 1)
    {
        _target.Add(new StatusEntry(statusId, remaining, stacks));
        return this;
    }

    public SnapshotBuilder Moving(bool moving = true, float forSeconds = 2f)
    {
        _snapshot.IsMoving = moving;
        _snapshot.MovingFor = moving ? forSeconds : 0f;
        _snapshot.StillFor = moving ? 0f : forSeconds;
        return this;
    }

    public SnapshotBuilder Downtime(bool downtime = true)
    {
        _snapshot.InDowntime = downtime;
        return this;
    }

    public SnapshotBuilder OutOfCombat()
    {
        _snapshot.InCombat = false;
        _snapshot.CombatDuration = 0f;
        return this;
    }

    public SnapshotBuilder Position(RelativePosition position)
    {
        _snapshot.Position = position;
        return this;
    }

    public SnapshotBuilder At(double now)
    {
        _snapshot.Now = now;
        return this;
    }

    public SnapshotBuilder Gauge(Action<CombatSnapshot> configure)
    {
        configure(_snapshot);
        return this;
    }

    public CombatSnapshot Build()
    {
        _snapshot.SelfStatuses = _self;
        _snapshot.TargetStatuses = _target;
        return _snapshot;
    }
}
