using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Engine;

/// <summary>
/// One entry in a priority list: "use this action, when this is true".
/// </summary>
public sealed class Rule
{
    private readonly Func<RotationContext, ActionRef?> _resolve;
    private Func<RotationContext, bool>? _condition;
    private Func<RotationContext, string>? _noteResolver;

    internal Rule(ActionKind kind, Func<RotationContext, ActionRef?> resolve)
    {
        Kind = kind;
        _resolve = resolve;
    }

    public ActionKind Kind { get; }

    public string? Note { get; private set; }

    public PositionalHint Positional { get; private set; } = PositionalHint.None;

    /// <summary>Adds the guard clause. A rule with no guard always fires when its action is ready.</summary>
    public Rule When(Func<RotationContext, bool> condition)
    {
        _condition = _condition is null
            ? condition
            : Combine(_condition, condition);

        return this;
    }

    /// <summary>Short explanation shown in the HUD when this rule fires.</summary>
    public Rule Because(string note)
    {
        Note = note;
        return this;
    }

    /// <summary>
    /// Explanation that depends on why the rule fired - "instant, you are moving" versus
    /// "coils are close to capping" for the same action.
    /// </summary>
    public Rule Because(Func<RotationContext, string> note)
    {
        _noteResolver = note;
        return this;
    }

    /// <summary>The note to show, resolving the dynamic form if there is one.</summary>
    internal string? NoteFor(RotationContext context) =>
        _noteResolver is not null ? _noteResolver(context) : Note;

    /// <summary>Marks the action as wanting a positional, so the HUD can hint it.</summary>
    public Rule Needs(PositionalHint positional)
    {
        Positional = positional;
        return this;
    }

    /// <summary>Restricts the rule to a level range, for synced content.</summary>
    public Rule AtLevel(byte min, byte max = 255) =>
        When(c => c.Level >= min && c.Level <= max);

    /// <summary>Evaluates the rule. Returns null when it does not apply.</summary>
    internal ActionRef? Evaluate(RotationContext context)
    {
        var action = _resolve(context);
        if (action is null)
            return null;

        if (!context.Ready(action))
            return null;

        if (_condition is not null && !_condition(context))
            return null;

        return action;
    }

    private static Func<RotationContext, bool> Combine(
        Func<RotationContext, bool> first,
        Func<RotationContext, bool> second) =>
        c => first(c) && second(c);
}
