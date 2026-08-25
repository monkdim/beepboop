namespace TwoButton.Core.Model;

/// <summary>
/// Job gauge values, flattened into plain structs so the rotation engine never has to
/// reference Dalamud's gauge types. The plugin maps the real gauge onto these; only the
/// struct for the player's current job is meaningful.
/// </summary>
public sealed class JobGauges
{
    public DragoonGauge Dragoon;
    public MachinistGauge Machinist;
}

public struct DragoonGauge
{
    /// <summary>Firstminds' Focus stacks (0-2). Two stacks spends into Wyrmwind Thrust.</summary>
    public byte FirstmindsFocus;

    /// <summary>Seconds left on Life of the Dragon. Zero when not active.</summary>
    public float LotdTimeRemaining;

    public readonly bool LotdActive => LotdTimeRemaining > 0f;
}

public struct MachinistGauge
{
    /// <summary>Heat gauge, 0-100. Spends 50 into Hypercharge.</summary>
    public byte Heat;

    /// <summary>Battery gauge, 0-100. Spends 50+ into Automaton Queen.</summary>
    public byte Battery;

    /// <summary>Battery that was spent on the queen currently out.</summary>
    public byte LastSummonBatteryPower;

    public bool Overheated;

    public float OverheatTimeRemaining;

    /// <summary>True while Automaton Queen is active. Battery cannot be spent again.</summary>
    public bool RobotActive;

    public float SummonTimeRemaining;
}
