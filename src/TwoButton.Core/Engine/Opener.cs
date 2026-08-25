using TwoButton.Core.Model;

namespace TwoButton.Core.Engine;

/// <summary>
/// A scripted opening sequence. Openers are worth a lot of damage and are the hardest part
/// of a fight to execute by hand, so the engine walks the list step by step and bails out
/// the moment reality stops matching - it never fights the player for control.
/// </summary>
public sealed class Opener(string name, byte minimumLevel, params ActionRef[] steps)
{
    public string Name { get; } = name;

    /// <summary>Openers are written for a specific level. Below this, use the priority list.</summary>
    public byte MinimumLevel { get; } = minimumLevel;

    /// <summary>
    /// Step index to prompt the potion before. Most openers pre-pull or first-weave it.
    /// Negative means this opener has no potion point.
    /// </summary>
    public int PotionBeforeStep { get; init; } = -1;

    public IReadOnlyList<ActionRef> Steps { get; } = steps;
}
