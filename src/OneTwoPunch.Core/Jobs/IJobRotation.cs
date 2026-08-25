using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Jobs;

/// <summary>
/// One job's two priority lists, plus the metadata the plugin needs to hook it up.
/// </summary>
public interface IJobRotation
{
    /// <summary>ClassJob row id.</summary>
    uint JobId { get; }

    string Name { get; }

    /// <summary>
    /// The action the player physically puts on their hotbar for single target. Pressing it
    /// casts whatever the single-target plan resolves to.
    /// </summary>
    ActionRef SingleTargetButton { get; }

    /// <summary>The hotbar action for the AoE plan.</summary>
    ActionRef AoeButton { get; }

    /// <summary>Radius in yalms used to count enemies for the AoE button.</summary>
    float AoeRadius { get; }

    /// <summary>Every action the job's plans can suggest. Used for id verification.</summary>
    IReadOnlyList<ActionRef> AllActions { get; }

    /// <summary>Every status the job's plans read. Used for id verification.</summary>
    IReadOnlyList<StatusRef> AllStatuses { get; }

    /// <summary>
    /// The ability used to ignore a positional the player cannot reach - True North for
    /// melee, null for everyone else.
    /// </summary>
    ActionRef? PositionalRescue { get; }

    /// <summary>Status applied by <see cref="PositionalRescue"/>, so it is not double-pressed.</summary>
    StatusRef? PositionalRescueStatus { get; }

    /// <summary>
    /// The buff that marks this job's burst window, if it has one. Used to time the potion
    /// prompt outside the opener.
    /// </summary>
    StatusRef? BurstStatus { get; }

    /// <summary>
    /// The ability that opens this job's burst. The potion prompt also fires when this is
    /// what the button has become - which works for every job, including the many whose
    /// burst is not marked by a buff on the player.
    /// </summary>
    ActionRef? BurstAction { get; }

    Opener? Opener { get; }

    RotationPlan SingleTarget { get; }

    RotationPlan Aoe { get; }

    /// <summary>
    /// Optional third and fourth buttons, for mechanics the two main buttons cannot express.
    /// Empty for most jobs.
    /// </summary>
    IReadOnlyList<ExtraButton> ExtraButtons { get; }
}
