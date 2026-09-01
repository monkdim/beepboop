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

    /// <summary>
    /// How many enemies the AoE list is a gain over the single-target one. Below this, the
    /// AoE button falls back to single target when that setting is on. Two for most jobs;
    /// three for one whose area combos are written for three.
    /// </summary>
    int AoeMinimumEnemies { get; }

    /// <summary>
    /// The fewest off-globals per window this job's rotation is written for, applied over
    /// the player's weave setting when theirs is lower. <see cref="WeaveStyle.None"/> means
    /// the job asks nothing beyond what the player chose.
    /// <para>
    /// The accessible default is one weave per window, and for most jobs that costs a little
    /// damage and nothing else. Viper's coils and Uncoiled Fury each hand out two off-globals
    /// that must both go out before the next global, so on one weave the job silently drops
    /// half of them - about a sixth of its off-global damage. A player who chose "globals
    /// only" is still honoured: the minimum only raises a setting that already allows weaving.
    /// </para>
    /// </summary>
    WeaveStyle MinimumWeaveStyle { get; }

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

    /// <summary>
    /// The job's own gauge in one short line, for the recorder. Null when the job has
    /// nothing worth saying.
    /// <para>
    /// A recorded Reaper pull could not answer why the second Enshroud never happened,
    /// because Soul and Shroud - the two numbers the whole rotation turns on - left no
    /// trace anywhere. Buffs and cooldowns were all there; the gauge was invisible.
    /// </para>
    /// </summary>
    string? DescribeGauge(CombatSnapshot snapshot);

    Opener? Opener { get; }

    RotationPlan SingleTarget { get; }

    RotationPlan Aoe { get; }

    /// <summary>
    /// Optional third and fourth buttons, for mechanics the two main buttons cannot express.
    /// Empty for most jobs.
    /// </summary>
    IReadOnlyList<ExtraButton> ExtraButtons { get; }
}
