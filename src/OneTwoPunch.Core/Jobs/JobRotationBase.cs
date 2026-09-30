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
    /// <para>
    /// A trailing <c>!580</c> is the one that decides a global. The <c>#</c> code answers "is
    /// it acceptable this instant", which is no for most of every global whether or not
    /// anything is wrong, so it cannot tell an ordinary rolling cooldown from a rule that can
    /// never fire. The <c>!</c> code answers the question a global is actually judged by -
    /// acceptable apart from the recast - and only appears when that one is refused. A whole
    /// log of Pictomancer motifs reading <c>#582</c> said nothing; one of them reading
    /// <c>!580</c> would have said everything.
    /// </para>
    /// </summary>
    /// <summary>
    /// The action the game currently offers in place of this one, as a declared
    /// <see cref="ActionRef"/> rather than a bare id.
    /// <para>
    /// Some actions are never castable as themselves. Pictomancer's Creature Motif is the
    /// icon on the bar and the game always replaces it - with Pom, Wing, Claw or Maw
    /// depending on the portraits - and asking the game about the id on the bar gets "cannot
    /// use yet" no matter what the canvas holds. A recorded pull shows the pair side by side:
    /// Living Muse refused at three charges with the creature canvas drawn, while Striking
    /// Muse, named by its replaced id, went off in the same fight. Every action in that log
    /// reading a flat refusal has an ActionIndirection row; every action that fires has none.
    /// </para>
    /// <para>
    /// So rules for those name the id on the bar and resolve it here, which is the same
    /// "ask the game, do not track state" the mudras and Beastmaster's ring are built on -
    /// and it answers with the form the hook would have handed back anyway.
    /// </para>
    /// </summary>
    protected ActionRef Current(RotationContext context, ActionRef action) =>
        Resolve(action, context.CurrentFormOf(action));

    /// <summary>The same, for the readiness line, which has no context.</summary>
    protected ActionRef Current(IActionState actions, ActionRef action) =>
        Resolve(action, actions.CurrentFormOf(action.Id));

    private Dictionary<uint, ActionRef>? _byId;

    private ActionRef Resolve(ActionRef action, uint id)
    {
        if (id == 0 || id == action.Id)
            return action;

        if (_byId is null)
        {
            _byId = [];
            var all = AllActions;
            for (var i = 0; i < all.Count; i++)
                _byId.TryAdd(all[i].Id, all[i]);
        }

        // An undeclared form is left as the id on the bar rather than suggested unchecked -
        // the verifier has never seen it, and the smoke test requires every suggestion to be
        // an action the job declares.
        return _byId.TryGetValue(id, out var form) ? form : action;
    }

    /// <summary>
    /// The probe for an action the game replaces, naming both: <c>Creature Motif->Wing
    /// Motif=y1/1</c>. Probing the id on the bar alone is how two versions went by with the
    /// motifs reading a refusal that belonged to an id nothing would ever cast.
    /// </summary>
    protected string ProbeCurrent(IActionState actions, ActionRef action)
    {
        var form = Current(actions, action);

        return form.Id == action.Id
            ? Probe(actions, action)
            : $"{action.Name}->{Probe(actions, form)}";
    }

    protected static string Probe(IActionState actions, ActionRef action)
    {
        var usable = actions.CanUse(action.Id);
        var charges = $"{actions.ChargesAvailable(action.Id)}/{actions.MaxCharges(action.Id)}";
        var cd = actions.CooldownRemaining(action.Id);
        var left = cd > 0.05f ? $":{cd:0.0}" : string.Empty;
        var why = usable ? string.Empty : $"#{actions.RefusalCode(action.Id)}";

        var next = actions.CanUse(action.Id, ignoreRecast: true);
        var blocked = next ? string.Empty : $"!{actions.RefusalCode(action.Id, ignoreRecast: true)}";

        return $"{action.Name}={(usable ? "y" : "n")}{charges}{left}{why}{blocked}";
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
