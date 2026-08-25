using TwoButton.Core.Engine;
using TwoButton.Core.Model;

namespace TwoButton.Core.Jobs;

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

    public abstract IReadOnlyList<ActionRef> AllActions { get; }

    public abstract IReadOnlyList<StatusRef> AllStatuses { get; }

    public virtual ActionRef? PositionalRescue => null;

    public virtual StatusRef? PositionalRescueStatus => null;

    public virtual StatusRef? BurstStatus => null;

    public virtual ActionRef? BurstAction => null;

    public virtual Opener? Opener => null;

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
