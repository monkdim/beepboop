using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Viper;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Viper.ViperActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Which sting follows the combo starter.
/// <para>
/// The venom finisher buff decides it, because it names the finisher and each sting leads
/// to only two of the four - Hunter's Sting to the flank pair, Swiftskin's Sting to the rear
/// pair. Take the other one and "it is no longer possible to press the buffed combo
/// finisher", and since every finisher rule keys on the venom, nothing matches and the
/// list restarts the combo at Steel Fangs.
/// </para>
/// <para>
/// It used to be decided by whichever self-buff was closer to dropping. In full uptime the
/// two agree by accident - a recorded 4:29 pull is 17 for 17 - and part company after forty
/// to sixty seconds of downtime, when the 40s self-buffs have gone and the 60s venom has not.
/// </para>
/// </summary>
public sealed class ViperStingTests
{
    private static uint Suggest(uint? venom, float swiftscaled, float huntersInstinct)
    {
        var builder = new SnapshotBuilder()
            .Job(41).Level(100).Gcd(0f).Enemies(1)
            .Combo(A.SteelFangs.Id);

        if (venom is { } v)
            builder.Buff(v, 30f);

        if (swiftscaled > 0f)
            builder.Buff(A.Swiftscaled.Id, swiftscaled);

        if (huntersInstinct > 0f)
            builder.Buff(A.HuntersInstinct.Id, huntersInstinct);

        // At the sting step the game will not take a finisher - the branch has not been
        // chosen yet. The fake takes everything, and the finisher rules sit above the stings.
        var actions = new FakeActionState()
            .Unusable(A.FlankstingStrike.Id)
            .Unusable(A.FlanksbaneFang.Id)
            .Unusable(A.HindstingStrike.Id)
            .Unusable(A.HindsbaneFang.Id);

        return new RotationSession(JobRotationBase.Create<ViperRotation>(),
            new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f })
            .Resolve(RotationMode.SingleTarget, builder.Build(), actions)
            .Action.Id;
    }

    /// <summary>
    /// The defect, in the shape it takes after downtime: both self-buffs gone, so the old
    /// tie-break says Swiftskin's - and the venom held wants a flank finisher.
    /// </summary>
    [Theory]
    [InlineData(3645u)] // Flankstung Venom
    [InlineData(3646u)] // Flanksbane Venom
    public void AFlankVenomTakesHuntersStingEvenWhenSwiftscaledIsTheOneMissing(uint venom)
    {
        Assert.Equal(A.HuntersSting.Id, Suggest(venom, swiftscaled: 0f, huntersInstinct: 30f));
    }

    /// <summary>And the mirror: a rear venom takes Swiftskin's Sting whatever the self-buffs say.</summary>
    [Theory]
    [InlineData(3647u)] // Hindstung Venom
    [InlineData(3648u)] // Hindsbane Venom
    public void ARearVenomTakesSwiftskinsStingEvenWhenHuntersInstinctIsTheOneMissing(uint venom)
    {
        Assert.Equal(A.SwiftskinsSting.Id, Suggest(venom, swiftscaled: 30f, huntersInstinct: 0f));
    }

    /// <summary>
    /// With no venom held - the first combo, coming back from a death - the self-buff closer
    /// to dropping decides, as it always did.
    /// </summary>
    [Fact]
    public void WithNoVenomHeldTheShorterSelfBuffDecides()
    {
        Assert.Equal(A.SwiftskinsSting.Id, Suggest(null, swiftscaled: 5f, huntersInstinct: 30f));
        Assert.Equal(A.HuntersSting.Id, Suggest(null, swiftscaled: 30f, huntersInstinct: 5f));
    }
}
