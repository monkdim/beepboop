# Design

## The core mechanism

The game constantly asks itself `GetAdjustedActionId(slot)` — *"what should this hotbar
slot actually cast?"* That is how one button becomes the next step of a combo, and how
Heat Blast becomes Blazing Shot at level 86.

Two Button hooks that question and answers it. That is the whole trick, and it is what
makes this a rotation *helper* rather than an automation plugin:

- The plugin is **asked**, it does not act. It runs when the game renders an icon.
- The answer is passed back through the game's own adjustment
  (`_hook.Original(manager, suggestedId)`), so upgrades and procs still resolve natively
  instead of being duplicated in our tables.
- Only the two host action ids are ever intercepted. Every other action is passed through
  untouched, so the rest of the hotbar behaves exactly as it always did.
- A crash in the rotation is caught and the original id returned. A bug here must never
  take the player's hotbar with it.

## Why the engine has no Dalamud dependency

`TwoButton.Core` is plain C# and references nothing from the game. Everything it is allowed
to know arrives as a `CombatSnapshot` plus an `IActionState`.

That is not tidiness for its own sake. It means the rotation logic — the part that is
actually easy to get wrong — is unit tested in CI on every push, by anyone, without the
game installed. The Dalamud layer is deliberately thin and boring: fill in a snapshot,
answer the hook, draw a window.

## Resolving a button

Every frame, for the button being drawn:

1. **Track the GCD window.** A global cooldown that jumped back up means a fresh window, so
   the weave budget resets.
2. **Pick the mode.** If this is the AoE button and only one enemy is in range, quietly use
   the single-target plan instead.
3. **Run the opener**, if one is armed. It overrides the priority list, and gives up the
   moment reality stops matching.
4. **Resolve the next GCD** — always, even when we are about to suggest an off-global. It
   is the look-ahead shown in the HUD, the fallback when no weave fits, and what lets an
   off-global rule say *"Reassemble, but only in front of a tool."*
5. **If a weave fits**, offer the first matching off-global (positional rescue first).
6. **Otherwise** the button is the next GCD — shown even while the GCD is still rolling, so
   it can be pressed into the game's own input queue rather than reacted to.
7. **Stabilise**, so the icon cannot change under somebody's hand.

Steps 1 and 7 are the only stateful ones. `Resolve` itself is side-effect free and safe to
call every frame; state changes only when the plugin reports that an action actually went
off.

## The anti-clip guarantee

The one promise the engine keeps is that **pressing the button can never cost you a global
cooldown**. An off-global is offered only when:

```
gcdRemaining >= animationLockAlreadyRunning + assumedAnimationLock + safetyMargin
```

and the weave budget for this window has not been spent. The stabiliser is not allowed to
hold an off-global past the point where that stops being true.

This is also the accessibility lever. `WeaveStyle.None` / `Single` / `Double` is not a
difficulty setting — it is how many presses per GCD the plugin is allowed to ask of you.

## Priority lists

A job is two ordered lists. The first rule whose action is usable and whose condition holds
wins. `Rule.Evaluate` gates every rule through `context.Ready(action)`, which includes the
game's own `GetActionStatus` — so a suggestion can never be something that would just make
an error noise.

```csharp
p.OGcd(A.LifeSurge)
    .When(c => !c.Buff(A.LifeSurgeBuff)
               && c.Buff(A.LanceChargeBuff)
               && c.GcdImminent
               && c.NextGcdIsAny(A.Drakesbane, A.HeavensThrust, A.FullThrust))
    .Because("guaranteed crit on the big hit");
```

The intent is that a raider who does not write C# can still read a rotation file and tell
you whether it is wrong.

## Action id safety

Hard-coded ids are the most likely thing in this repository to be wrong. They get shuffled
by patches and mistyped by contributors, and a wrong one is invisible until it casts the
wrong ability in a fight that matters.

So the id is treated as a guess and **the name is the real identity**:

- At startup every id is checked against the game's Action/Status sheets.
- A mismatch is **repaired by name** and logged.
- A name that cannot be resolved at all **disables the job** with a message. Guessing is
  worse than being off.

`/twobutton verify` re-runs the whole check and prints it, which is the thing to run after
a patch and the thing to paste into a bug report.

The action tables are `static readonly`, which makes them process-global mutable state
after a rebind. That is fine at runtime — one process, verified once at load — but it is
why the test suite disables xUnit's parallelism.

## Deliberate omissions

- **No input is ever sent.** No queuing, no auto-target, no timing assistance. If that ever
  becomes tempting, it belongs in a different plugin with a different name.
- **No damage-optimal-at-all-costs mode.** Where the two conflict, predictability wins.
  A button that surprises you is worse than a button that loses a percent.
- **The AoE button does not decide it knows better** than single target when you have
  explicitly pressed AoE on a pack; the fallback only triggers on a genuinely lone enemy,
  and can be turned off.

## Known soft spots

- `PositionMath` depends on the target-facing convention. It only feeds the True North
  hint — a wrong convention costs a hint, never a rotation decision — and it can be turned
  off. Worth confirming in game.
- `ActionUseWatcher` infers "an action was used" from a cooldown jumping up rather than
  hooking `UseAction`. That is patch-proof but one frame late, which the GCD-rollover reset
  covers.
- Charge counting is derived from recast progress rather than read directly, because the
  direct accessor has moved between Dalamud versions.
