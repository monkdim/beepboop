namespace TwoButton.Core.Model;

/// <summary>Which of the two buttons is being resolved.</summary>
public enum RotationMode
{
    SingleTarget,
    Aoe,
}

/// <summary>Positional requirement of a suggested action, surfaced to the HUD as a hint.</summary>
public enum PositionalHint
{
    None,
    Flank,
    Rear,
}

/// <summary>Where the player is standing relative to the target.</summary>
public enum RelativePosition
{
    Unknown,
    Front,
    Flank,
    Rear,
}

/// <summary>One entry from a status (buff/debuff) list.</summary>
public readonly struct StatusEntry(uint id, float remaining, byte stacks)
{
    public uint Id { get; } = id;

    /// <summary>Seconds left. Permanent statuses report <see cref="float.PositiveInfinity"/>.</summary>
    public float Remaining { get; } = remaining;

    public byte Stacks { get; } = stacks;
}

/// <summary>
/// Everything the rotation engine is allowed to know about the world, captured once per
/// resolve. The plugin layer fills this in from Dalamud; tests fill it in by hand.
/// </summary>
public sealed class CombatSnapshot
{
    /// <summary>ClassJob row id (e.g. 22 = Dragoon).</summary>
    public uint JobId { get; set; }

    public byte Level { get; set; }

    public bool InCombat { get; set; }

    /// <summary>Seconds the player has been in combat. Drives opener tracking.</summary>
    public float CombatDuration { get; set; }

    /// <summary>Monotonic time source in seconds. Only differences are meaningful.</summary>
    public double Now { get; set; }

    // ---- Global cooldown -------------------------------------------------

    /// <summary>Full recast of the player's GCD at current spell/skill speed.</summary>
    public float GcdTotal { get; set; } = 2.5f;

    /// <summary>Seconds until the next GCD can be used. Zero when the GCD is up.</summary>
    public float GcdRemaining { get; set; }

    /// <summary>Seconds of animation lock still to run. Blocks weaving.</summary>
    public float AnimationLock { get; set; }

    // ---- Targeting -------------------------------------------------------

    public bool HasTarget { get; set; }

    public bool TargetIsHostile { get; set; }

    public bool TargetInRange { get; set; }

    public float TargetHpFraction { get; set; } = 1f;

    /// <summary>
    /// Enemies that would be hit by the job's AoE around the current target. Counted by
    /// the plugin using the job's own AoE shape and radius.
    /// </summary>
    public int EnemiesInAoeRange { get; set; }

    public RelativePosition Position { get; set; } = RelativePosition.Unknown;

    /// <summary>
    /// True when there is nothing worth hitting: no hostile target, or the boss is
    /// untargetable. Used to hold burst instead of throwing it into a wall.
    /// </summary>
    public bool InDowntime { get; set; }

    // ---- Movement --------------------------------------------------------

    public bool IsMoving { get; set; }

    /// <summary>Seconds the player has been continuously moving. Zero when standing still.</summary>
    public float MovingFor { get; set; }

    /// <summary>Seconds the player has been standing still. Zero while moving.</summary>
    public float StillFor { get; set; }

    // ---- Combo -----------------------------------------------------------

    /// <summary>The last action that advanced a combo, or 0.</summary>
    public uint LastComboAction { get; set; }

    /// <summary>Seconds left on the combo timer. Zero when no combo is active.</summary>
    public float ComboTimeRemaining { get; set; }

    // ---- Statuses --------------------------------------------------------

    /// <summary>Buffs on the player.</summary>
    public IReadOnlyList<StatusEntry> SelfStatuses { get; set; } = [];

    /// <summary>
    /// Debuffs on the current target. The plugin filters this to statuses the player
    /// applied, so another Dragoon's Chaotic Spring never suppresses yours.
    /// </summary>
    public IReadOnlyList<StatusEntry> TargetStatuses { get; set; } = [];

    // ---- Potion ----------------------------------------------------------

    /// <summary>True when the configured potion is off cooldown and in the inventory.</summary>
    public bool PotionAvailable { get; set; }

    /// <summary>Seconds until the potion comes off cooldown.</summary>
    public float PotionCooldownRemaining { get; set; }

    // ---- Job gauges ------------------------------------------------------

    public JobGauges Gauges { get; } = new();
}
