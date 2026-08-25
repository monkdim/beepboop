# Adding a job

A job is two files and one line. You do not need to touch the engine.

## 1. The action table

`src/TwoButton.Core/Jobs/<Job>/<Job>Actions.cs`

```csharp
public static readonly ActionRef TrueThrust = new(75, "True Thrust", ActionKind.Gcd, 1);
public static readonly StatusRef PowerSurge = new(2720, "Power Surge");
```

- `ActionKind` decides whether the engine treats it as a global cooldown or a weave.
- The level is used to gate rules in synced content.
- **The name matters more than the id.** Ids are verified against the game's own sheets at
  startup and repaired by name when they do not match, so get the name exactly right and
  the id can be approximate. A name that cannot be resolved disables the whole job.
- Every action a rule can suggest must appear in the `All` list, and every status in
  `AllStatuses`. There is a test that enforces this.

## 2. The rotation

`src/TwoButton.Core/Jobs/<Job>/<Job>Rotation.cs`

```csharp
public sealed class ReaperRotation : JobRotationBase
{
    public override uint JobId => 39;
    public override string Name => "Reaper";
    public override ActionRef SingleTargetButton => A.Slice;
    public override ActionRef AoeButton => A.SpinningScythe;

    protected override void Build()
    {
        var p = SingleTarget;

        p.OGcd(A.ArcaneCircle).When(c => !c.Downtime).Because("raid buff");
        p.Gcd(A.InfernalSlice).When(c => c.ComboIs(A.WaxingSlice));
        p.Gcd(A.WaxingSlice).When(c => c.ComboIs(A.Slice));
        p.Gcd(A.Slice);
    }
}
```

Order is the whole specification: **first match wins**. Write finishers before their
prerequisites so the deepest live combo step wins.

The engine handles weave safety, the GCD/off-global split, the look-ahead, stabilisation
and the AoE fallback. A rule only has to say *what* and *when*.

### What a rule can ask

| | |
|---|---|
| `c.Ready(a)` | usable right now — off cooldown, learned, accepted by the game |
| `c.Has(a)` | learned at the current level |
| `c.Cd(a)`, `c.Charges(a)`, `c.ReadyIn(a, s)` | cooldown state |
| `c.ComboIs(a)` | the live combo step |
| `c.Buff(s)`, `c.BuffTime(s)`, `c.BuffStacks(s)` | own buffs |
| `c.Debuff(s)`, `c.DotExpiring(s, within)` | own debuffs on the target |
| `c.NextGcdIs(a)`, `c.NextGcdIsAny(...)` | what this weave would be buffing |
| `c.GcdImminent` | the next GCD is close enough for a buff to still be up |
| `c.Moving`, `c.MovingFor` | movement — swap to an instant |
| `c.Enemies` | enemies in range (always 1 on the single-target plan) |
| `c.Downtime` | boss untargetable; hold burst |
| `c.Position` | where you are standing, for positionals |
| `c.Level`, `c.InRange`, `c.TargetHp` | the obvious ones |

`Ready` is applied automatically before your condition runs, so never write
`.When(c => c.Ready(A.Thing))` for the action the rule is already about.

### Movement

This is the one that matters most for the people this plugin is for. A caster's list wants
a movement branch near the top:

```csharp
p.Gcd(A.Xenoglossy)
    .When(c => c.Moving && c.BuffStacks(A.Polyglot) > 0)
    .Because("instant, you are moving");
```

`c.Moving` is latched: it turns on immediately and off only after a short settle, so a
single stationary frame mid-strafe cannot make the button flicker.

### Positionals

`.Needs(PositionalHint.Rear)` on a GCD rule does two things: it shows the hint in the HUD,
and it lets the engine offer True North when you are standing in the wrong place. Declare
`PositionalRescue` and `PositionalRescueStatus` on the job for that to work.

### Openers

```csharp
private static readonly Opener Sequence = new("Dawntrail standard", 100, A.Step1, A.Step2, ...);
public override Opener? Opener => Sequence;
```

Cache it in a static — do not build one per property access. The engine walks it, gives up
the moment the player does something else, and never starts one mid-fight.

## 3. Register it

`src/TwoButton.Core/Jobs/JobRegistry.cs`

```csharp
JobRotationBase.Create<ReaperRotation>,
```

## 4. Test it

Tests run without the game. Copy the shape from `tests/TwoButton.Core.Tests`:

```csharp
[Fact]
public void TheComboFinisherComesOutLast()
{
    var session = new RotationSession(
        JobRotationBase.Create<ReaperRotation>(),
        new RotationSettings { UseOpener = false });

    var snapshot = new SnapshotBuilder().Gcd(0.1f).Combo(A.WaxingSlice).Build();
    var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

    Assert.Equal(A.InfernalSlice.Id, suggestion.Action.Id);
}
```

Worth covering: the combo walks in order, burst is held during downtime, the AoE button
does something sensible at one enemy and at five, and any gauge-overcap rule actually
fires.
