using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.BlackMage;
using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The opener is transcribed from The Balance's Standard "5+7" chart for Black Mage level
/// 100, Dawntrail patch 7.2. It is a transcription, not a derivation, so the thing worth
/// testing is that it still says what the chart says.
/// </summary>
public sealed class BlackMageOpenerTests
{
    /// <summary>The twenty-four numbered globals, in the chart's order.</summary>
    private static readonly ActionRef[] ChartGcds =
    [
        BlackMageActions.Fire3,        //  1
        BlackMageActions.HighThunder,  //  2
        BlackMageActions.Fire4,        //  3
        BlackMageActions.Fire4,        //  4
        BlackMageActions.Fire4,        //  5
        BlackMageActions.Fire4,        //  6
        BlackMageActions.Fire4,        //  7
        BlackMageActions.Xenoglossy,   //  8
        BlackMageActions.Fire4,        //  9
        BlackMageActions.FlareStar,    // 10
        BlackMageActions.Fire4,        // 11
        BlackMageActions.Fire4,        // 12
        BlackMageActions.HighThunder,  // 13
        BlackMageActions.Fire4,        // 14
        BlackMageActions.Fire4,        // 15
        BlackMageActions.Fire4,        // 16
        BlackMageActions.Fire4,        // 17
        BlackMageActions.FlareStar,    // 18
        BlackMageActions.Despair,      // 19
        BlackMageActions.Blizzard3,    // 20
        BlackMageActions.Blizzard4,    // 21
        BlackMageActions.Paradox,      // 22
        BlackMageActions.Paradox,      // 23
        BlackMageActions.Fire3,        // 24, the Firestarter proc
    ];

    /// <summary>The off-globals, each after the global it is woven into.</summary>
    private static readonly ActionRef[] ChartWeaves =
    [
        BlackMageActions.Swiftcast,
        BlackMageActions.Amplifier,
        BlackMageActions.LeyLines,
        BlackMageActions.Manafont,
        BlackMageActions.Transpose,
        BlackMageActions.Triplecast,
        BlackMageActions.Transpose,
    ];

    private static Opener TheOpener =>
        JobRegistry.Create(25)!.Opener ?? throw new InvalidOperationException("Black Mage has no opener");

    [Fact]
    public void TheGlobalsMatchTheChart()
    {
        var globals = TheOpener.Steps.Where(s => s.Kind == ActionKind.Gcd).ToArray();

        Assert.Equal(ChartGcds.Length, globals.Length);

        for (var i = 0; i < ChartGcds.Length; i++)
        {
            Assert.True(
                ChartGcds[i].Id == globals[i].Id,
                $"step {i + 1}: chart says {ChartGcds[i].Name}, opener says {globals[i].Name}");
        }
    }

    [Fact]
    public void TheWeavesMatchTheChart()
    {
        var weaves = TheOpener.Steps.Where(s => s.Kind == ActionKind.OGcd).ToArray();

        Assert.Equal(ChartWeaves.Length, weaves.Length);

        for (var i = 0; i < ChartWeaves.Length; i++)
            Assert.True(ChartWeaves[i].Id == weaves[i].Id, $"weave {i + 1}: {weaves[i].Name}");
    }

    /// <summary>
    /// Five Fire IVs before Xenoglossy and seven after. That is what the name means, and it
    /// is the quickest way to notice a transcription slip.
    /// </summary>
    [Fact]
    public void ItIsFiveThenSeven()
    {
        var globals = TheOpener.Steps.Where(s => s.Kind == ActionKind.Gcd).ToArray();
        var xenoglossy = Array.FindIndex(globals, a => a.Id == BlackMageActions.Xenoglossy.Id);

        Assert.True(xenoglossy > 0, "Xenoglossy is not in the opener");

        var before = globals.Take(xenoglossy).Count(a => a.Id == BlackMageActions.Fire4.Id);
        var after = globals.Skip(xenoglossy).Count(a => a.Id == BlackMageActions.Fire4.Id);

        Assert.Equal(5, before);
        Assert.Equal(7, after);
    }

    /// <summary>The potion goes in Ley Lines' weave window, which is the step after it.</summary>
    [Fact]
    public void ThePotionIsInTheLeyLinesWindow()
    {
        var opener = TheOpener;
        Assert.True(opener.PotionBeforeStep >= 0, "no potion point");

        var atPotion = opener.Steps[opener.PotionBeforeStep];
        Assert.True(
            atPotion.Id == BlackMageActions.LeyLines.Id,
            $"potion is before {atPotion.Name}, expected Ley Lines");
    }
}
