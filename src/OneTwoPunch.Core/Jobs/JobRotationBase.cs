using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Jobs;

/// <summary>Shared plumbing so a job file is nothing but its two priority lists.</summary>
public abstract class JobRotationBase : IJobRotation
{
    protected JobRotationBase()
    {
        SingleTarget = new RotationPlan();
        Aoe = new RotationPlan();
    }

    public abstract uint JobId { get; }

    public abstract string Name { get; }

    public abstract ActionRef SingleTargetButton { get; }

    public abstract ActionRef AoeButton { get; }

    public virtual float AoeRadius => 5f;

    public virtual int AoeMinimumEnemies => 2;

    public virtual WeaveStyle MinimumWeaveStyle => WeaveStyle.None;

    public abstract IReadOnlyList<ActionRef> AllActions { get; }

    public abstract IReadOnlyList<StatusRef> AllStatuses { get; }

    public virtual ActionRef? PositionalRescue => null;

    public virtual StatusRef? PositionalRescueStatus => null;

    public virtual StatusRef? BurstStatus => null;

    public virtual ActionRef? BurstAction => null;

    public virtual Opener? Opener => null;

    /// <summary>
    /// The job's own gauge in one short line, for the recorder. Null unless the job says
    /// otherwise - see the note on <see cref="IJobRotation.DescribeGauge"/>.
    /// <para>
    /// Virtual here rather than a default on the interface: a default interface member
    /// cannot be overridden by an implementing class, which is what the first attempt at
    /// this tried and what CI rejected.
    /// </para>
    /// </summary>
    public virtual string? DescribeGauge(CombatSnapshot snapshot) => null;

    /// <summary>
    /// See <see cref="IJobRotation.DescribeReadiness"/>. Null for jobs that have not needed
    /// it; a job earns one the moment a rule of its own goes quiet with no visible reason.
    /// </summary>
    public virtual string? DescribeReadiness(CombatSnapshot snapshot, IActionState actions) => null;

    /// <summary>
    /// One action's readiness, in the facts that decide whether a rule may offer it:
    /// accepted by the game right now, charges in hand, cooldown left, and - when it is
    /// refused - the game's own reason.
    /// <para>
    /// Reads as <c>Ten=y2/2</c> for usable with two charges of two and no cooldown, or
    /// <c>Ten=n0/2:20.0#566</c> for refused, empty, twenty seconds to go, with 566 as the
    /// game's stated reason. The code is the part that matters: "refused" is one bit and the
    /// reasons behind it are many and unalike, which is how a self-targeted action being
    /// asked about against a hostile target went three rounds undiagnosed.
    /// </para>
    /// </summary>
    protected static string Probe(IActionState actions, ActionRef action)
    {
        var usable = actions.CanUse(action.Id);
        var charges = $"{actions.ChargesAvailable(action.Id)}/{actions.MaxCharges(action.Id)}";
        var cd = actions.CooldownRemaining(action.Id);
        var left = cd > 0.05f ? $":{cd:0.0}" : string.Empty;
        var why = usable ? string.Empty : $"#{actions.RefusalCode(action.Id)}";

        return $"{action.Name}={(usable ? "y" : "n")}{charges}{left}{why}";
    }

    public RotationPlan SingleTarget { get; }

    public RotationPlan Aoe { get; }

    private readonly List<ExtraButton> _extraButtons = [];

    public IReadOnlyList<ExtraButton> ExtraButtons => _extraButtons;

    /// <summary>Declares an extra button. Call from <see cref="Build"/>.</summary>
    protected ExtraButton AddExtraButton(
        ActionRef host,
        string name,
        string purpose,
        bool respectWeaveWindow = false)
    {
        if (_extraButtons.Count >= 2)
            throw new InvalidOperationException("A job may declare at most two extra buttons.");

        var button = new ExtraButton(host, name, purpose, respectWeaveWindow);
        _extraButtons.Add(button);
        return button;
    }

    /// <summary>
    /// Called once after construction to populate the plans. Split out from the constructor
    /// so derived classes can finish initialising their static tables first.
    /// </summary>
    protected abstract void Build();

    /// <summary>Creates and fully initialises a rotation.</summary>
    public static T Create<T>() where T : JobRotationBase, new()
    {
        var rotation = new T();
        rotation.Build();
        return rotation;
    }
}
