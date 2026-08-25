namespace TwoButton.Core.Model;

/// <summary>
/// Job gauge values, flattened into plain structs so the rotation engine never has to
/// reference Dalamud's gauge types. The plugin maps the real gauge onto these; only the
/// struct for the player's current job is meaningful.
/// <para>
/// Fields are deliberately limited to what the rotations actually read. Adding a field here
/// means adding one line to <c>GameStateProvider.FillGauges</c>.
/// </para>
/// </summary>
public sealed class JobGauges
{
    public MonkGauge Monk;
    public DragoonGauge Dragoon;
    public BardGauge Bard;
    public BlackMageGauge BlackMage;
    public SummonerGauge Summoner;
    public NinjaGauge Ninja;
    public MachinistGauge Machinist;
    public SamuraiGauge Samurai;
    public RedMageGauge RedMage;
    public DancerGauge Dancer;
    public ReaperGauge Reaper;
    public ViperGauge Viper;
    public PictomancerGauge Pictomancer;
}

// ---- Melee ---------------------------------------------------------------

public struct MonkGauge
{
    /// <summary>Chakra stacks, 0-5. Five spends into The Forbidden Chakra.</summary>
    public byte Chakra;

    /// <summary>Beast chakra opened so far, 0-3. Three enables a Blitz.</summary>
    public byte BeastChakraCount;

    /// <summary>All three beast chakra match, which makes the Blitz an Elixir Burst.</summary>
    public bool BeastChakraMatching;

    /// <summary>Raw Nadi flags. Compared as a value so the engine needs no Dalamud enum.</summary>
    public byte NadiFlags;

    public float BlitzTimeRemaining;

    /// <summary>Opo-opo fury stacks - Dawntrail's form-fury counters.</summary>
    public int OpoOpoFury;

    public int RaptorFury;

    public int CoeurlFury;
}

public struct DragoonGauge
{
    /// <summary>Firstminds' Focus stacks (0-2). Two stacks spends into Wyrmwind Thrust.</summary>
    public byte FirstmindsFocus;

    /// <summary>Seconds left on Life of the Dragon. Zero when not active.</summary>
    public float LotdTimeRemaining;

    public readonly bool LotdActive => LotdTimeRemaining > 0f;
}

public struct NinjaGauge
{
    /// <summary>Ninki, 0-100. Spends 50 into Bhavacakra or Hellfrog Medium.</summary>
    public byte Ninki;

    /// <summary>Kazematoi stacks, 0-5. Consumed by Aeolian Edge.</summary>
    public byte Kazematoi;
}

public struct SamuraiGauge
{
    /// <summary>Kenki, 0-100.</summary>
    public byte Kenki;

    /// <summary>Meditation stacks, 0-3. Three enables Ogi Namikiri.</summary>
    public byte Meditation;

    public bool HasSetsu;

    public bool HasGetsu;

    public bool HasKa;

    /// <summary>Sen held, 0-3. Three spends into Midare Setsugekka.</summary>
    public readonly int SenCount =>
        (HasSetsu ? 1 : 0) + (HasGetsu ? 1 : 0) + (HasKa ? 1 : 0);
}

public struct ReaperGauge
{
    /// <summary>Soul gauge, 0-100. Spends 50 into Blood Stalk / Grim Swathe.</summary>
    public byte Soul;

    /// <summary>Shroud gauge, 0-100. Spends 50 into Enshroud.</summary>
    public byte Shroud;

    public float EnshroudTimeRemaining;

    /// <summary>Lemure shroud, 0-5. The Void Reaping / Cross Reaping resource.</summary>
    public byte LemureShroud;

    /// <summary>Void shroud, 0-2. Two spends into Lemure's Slice.</summary>
    public byte VoidShroud;

    public readonly bool Enshrouded => EnshroudTimeRemaining > 0f;
}

public struct ViperGauge
{
    /// <summary>Rattling Coil stacks. Spent on Uncoiled Fury.</summary>
    public byte RattlingCoils;

    /// <summary>Serpent Offering, 0-100. Spends 50 into Reawaken.</summary>
    public byte SerpentOffering;

    /// <summary>Anguine Tribute, 0-5. The Reawaken combo counter.</summary>
    public byte AnguineTribute;
}

// ---- Physical ranged -----------------------------------------------------

public struct BardGauge
{
    /// <summary>Seconds left on the current song.</summary>
    public float SongTimeRemaining;

    /// <summary>Repertoire stacks for the active song.</summary>
    public byte Repertoire;

    /// <summary>Soul Voice, 0-100. Spends 50 into Apex Arrow.</summary>
    public byte SoulVoice;

    /// <summary>
    /// Raw song id, zero when no song is playing. Kept as a number rather than an enum so
    /// the engine stays free of Dalamud types.
    /// </summary>
    public byte SongId;

    /// <summary>Coda collected, 0-3. Three enables Radiant Finale.</summary>
    public byte CodaCount;

    public readonly bool AnySong => SongId != 0;
}

public struct MachinistGauge
{
    /// <summary>Heat gauge, 0-100. Spends 50 into Hypercharge.</summary>
    public byte Heat;

    /// <summary>Battery gauge, 0-100. Spends 50+ into Automaton Queen.</summary>
    public byte Battery;

    /// <summary>Battery that was spent on the queen currently out.</summary>
    public byte LastSummonBatteryPower;

    public bool Overheated;

    public float OverheatTimeRemaining;

    /// <summary>True while Automaton Queen is active. Battery cannot be spent again.</summary>
    public bool RobotActive;

    public float SummonTimeRemaining;
}

public struct DancerGauge
{
    /// <summary>Fourfold feathers, 0-4. Spent on Fan Dance.</summary>
    public byte Feathers;

    /// <summary>Esprit, 0-100. Spends 50 into Saber Dance.</summary>
    public byte Esprit;

    /// <summary>Steps completed in the current dance.</summary>
    public byte CompletedSteps;

    /// <summary>True while a Standard or Technical step is being danced.</summary>
    public bool Dancing;

    /// <summary>The step the dance currently wants, or 0 when not dancing.</summary>
    public uint NextStep;
}

// ---- Casters -------------------------------------------------------------

public struct BlackMageGauge
{
    /// <summary>Astral Fire stacks, 0-3.</summary>
    public byte AstralFire;

    /// <summary>Umbral Ice stacks, 0-3.</summary>
    public byte UmbralIce;

    /// <summary>Umbral hearts, 0-3.</summary>
    public byte UmbralHearts;

    /// <summary>Polyglot stacks. Spent on Xenoglossy or Foul.</summary>
    public byte PolyglotStacks;

    /// <summary>Seconds left on the current element. Zero outside Astral Fire / Umbral Ice.</summary>
    public float ElementTimeRemaining;

    /// <summary>Seconds until the next Polyglot stack.</summary>
    public float EnochianTimeRemaining;

    /// <summary>Astral soul stacks, 0-6. Six enables Flare Star.</summary>
    public byte AstralSoulStacks;

    public bool ParadoxActive;

    public readonly bool InAstralFire => AstralFire > 0;

    public readonly bool InUmbralIce => UmbralIce > 0;
}

public struct SummonerGauge
{
    /// <summary>Aetherflow stacks, 0-2.</summary>
    public byte AetherflowStacks;

    /// <summary>Attunement stacks for the active primal, 0-4.</summary>
    public byte Attunement;

    /// <summary>Seconds left on the active summon.</summary>
    public float SummonTimeRemaining;

    public bool IfritReady;

    public bool TitanReady;

    public bool GarudaReady;

    public bool BahamutReady;

    public bool PhoenixReady;

    public bool IfritAttuned;

    public bool TitanAttuned;

    public bool GarudaAttuned;

    /// <summary>True while a primal is out and its attunement is being spent.</summary>
    public readonly bool PrimalActive => Attunement > 0;
}

public struct RedMageGauge
{
    public byte WhiteMana;

    public byte BlackMana;

    /// <summary>Mana stacks from the melee combo, 0-3.</summary>
    public byte ManaStacks;

    public readonly int ManaDifference => Math.Abs(WhiteMana - BlackMana);

    public readonly int LowerMana => Math.Min(WhiteMana, BlackMana);
}

public struct PictomancerGauge
{
    /// <summary>Palette gauge, 0-100. Spends 50 into Subtractive Palette.</summary>
    public byte PaletteGauge;

    /// <summary>White paint stacks, spent on Holy in White / Comet in Black.</summary>
    public byte Paint;

    public bool CreatureMotifDrawn;

    public bool WeaponMotifDrawn;

    public bool LandscapeMotifDrawn;

    public bool MooglePortraitReady;

    public bool MadeenPortraitReady;
}
