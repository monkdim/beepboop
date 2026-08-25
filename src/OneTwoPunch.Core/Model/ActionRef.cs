namespace OneTwoPunch.Core.Model;

/// <summary>Whether an action rolls the global cooldown.</summary>
public enum ActionKind
{
    /// <summary>Weaponskill or spell. Rolls the GCD.</summary>
    Gcd,

    /// <summary>Off-global ability. Must be woven into a gap in the GCD.</summary>
    OGcd,
}

/// <summary>
/// A reference to a game action.
/// <para>
/// The <see cref="Id"/> here is a seed value. It is verified against the game's own
/// Action sheet at startup, and rebound by <see cref="Name"/> if it does not match,
/// so a stale or mistyped id self-corrects instead of silently casting the wrong
/// thing. See <c>docs/DESIGN.md</c> ("Action id safety").
/// </para>
/// </summary>
public sealed class ActionRef(uint id, string name, ActionKind kind, byte level = 1)
{
    public uint Id { get; private set; } = id;

    /// <summary>Canonical English name, used to verify and repair <see cref="Id"/>.</summary>
    public string Name { get; } = name;

    public ActionKind Kind { get; } = kind;

    /// <summary>Level the action is unlocked at. Used to gate rules for sync'd content.</summary>
    public byte Level { get; } = level;

    /// <summary>True once the id has been confirmed against the game's Action sheet.</summary>
    public bool Verified { get; private set; }

    /// <summary>True if <see cref="Bind"/> had to change the id to match the sheet.</summary>
    public bool WasRepaired { get; private set; }

    public void Bind(uint verifiedId)
    {
        if (verifiedId != Id)
        {
            Id = verifiedId;
            WasRepaired = true;
        }

        Verified = true;
    }

    public static implicit operator uint(ActionRef action) => action.Id;

    public override string ToString() => $"{Name} ({Id})";
}
