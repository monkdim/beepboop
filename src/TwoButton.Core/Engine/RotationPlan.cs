using TwoButton.Core.Model;

namespace TwoButton.Core.Engine;

/// <summary>
/// An ordered priority list for one button. Order is the whole specification: the first
/// rule whose action is usable and whose guard passes wins.
/// </summary>
public sealed class RotationPlan
{
    private readonly List<Rule> _rules = [];

    public IReadOnlyList<Rule> Rules => _rules;

    /// <summary>Adds a GCD to the priority list.</summary>
    public Rule Gcd(ActionRef action) => Add(ActionKind.Gcd, _ => action);

    /// <summary>Adds a GCD whose identity depends on game state (upgrades, procs, gauges).</summary>
    public Rule Gcd(Func<RotationContext, ActionRef?> resolve) => Add(ActionKind.Gcd, resolve);

    /// <summary>Adds an off-global. Only ever offered inside a safe weave window.</summary>
    public Rule OGcd(ActionRef action) => Add(ActionKind.OGcd, _ => action);

    public Rule OGcd(Func<RotationContext, ActionRef?> resolve) => Add(ActionKind.OGcd, resolve);

    private Rule Add(ActionKind kind, Func<RotationContext, ActionRef?> resolve)
    {
        var rule = new Rule(kind, resolve);
        _rules.Add(rule);
        return rule;
    }
}
