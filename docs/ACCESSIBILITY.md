# Accessibility notes

The plugin exists for players whose hands are the bottleneck, not their knowledge. The
design rules that follow from that are not obvious, so they are written down.

## Predictability beats damage

A button that surprises you is worse than a button that loses a percent. Where the two
conflict, predictability wins. Concretely:

- Suggestions are **held briefly** so the icon cannot change while somebody is reaching for
  the key — and dropped instantly if the held action stops being usable, so the hold can
  never cause a wasted press.
- The next GCD is shown **even while the GCD is still rolling**, so the button can be
  pressed into the game's own input queue rather than reacted to on a 100ms window.
- The **look-ahead** turns a changing button from something you react to into something you
  plan for. This is the single most useful feature for a fatigued or slow-reacting player,
  and it is why the panel is large and legible by default rather than a tasteful 24px.

## Presses per GCD is an accessibility setting

`WeaveStyle` is not a difficulty slider. Double-weaving is the single biggest driver of
actions-per-minute in this game, and it is exactly what a tremor, a splint or a fatigue
condition makes unreliable.

`None` is a ceiling on every job: nothing overrides globals-only, and that mode uses the
priority rotation rather than scripted pre-pull abilities.

Above that, a job may raise `Single` to `Double` where its rotation cannot work on one
weave. Viper, Ninja and Beastmaster do: their follow-ups come in pairs that must both go
out before the next global or they are lost, so on one weave the job drops one of every
pair - a recorded Viper duty read as the three-step combo and nothing else. Taking the
setting literally for those three was tried and reverted; if one weave per window is what
you need, choose `None` and the priority rotation will play to it, or say so and the pairs
can be made to fit a single slot instead.

Rotation settings apply to the active session on the next framework update, clearing held
suggestions while retaining used-weave counts.

On `Single` the engine drops the lowest-value off-globals first rather than clipping your
global cooldown. On `None` the plugin only ever asks you for globals. Both are supported
first-class; neither is a degraded mode.

## Never punish a mistimed press

Every suggestion passes through the game's own `GetActionStatus`, so the button is never an
action that would just make an error noise. A press that lands a beat early or late does
something reasonable rather than nothing.

The opener works the same way: it gives up the moment you press something else, rather than
jamming and waiting for you to get back in sync.

## Positionals

Standing behind a boss is a mobility requirement dressed up as a damage check. Where the
game offers an out — True North — the plugin offers it for you rather than asking you to
move, and the HUD says *"stand behind"* in plain words rather than an icon you have to
learn.

## What this plugin deliberately will not do

It will not press anything for you, queue anything, or time anything on your behalf. That
is not caution about the terms of service — it is the point. The people this was built for
asked to keep playing, not to be played for.

If somebody needs full automation, that is a legitimate need and there are plugins for it.
This is not that, and blurring the line would make it harder to defend as an accessibility
tool.
