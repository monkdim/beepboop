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
