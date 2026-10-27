# Changes

## 1.0.85.0

- Viper, Ninja and Beastmaster get the second weave their follow-ups need again. 1.0.83.0 made a job's weave minimum advisory, so on the default single-weave setting half of every Twinfang/Twinblood pair was dropped and the rotation read as the main combo and nothing else. "Globals only" still overrides every job, as it always did.
- A recorded log now says when the plugin was switched off, instead of listing every id on the bar as unrecognised. The suggestions are worked out whether or not the master switch is on, so a switched-off pull used to record a healthy-looking log in which not one press was the plugin's - and the footer blamed a level sync for it.

## 1.0.84.0

- Opener follow-ups wait through their existing 750 ms settling period before a missing prerequisite is treated as missing setup.
- Each step gets its own grace period, including Viper's Twinfang Bite and Twinblood Bite after Hunter's Coil.
- Missing prerequisites are not judged while a cast is in flight. Genuinely missing setup still lets the opener continue after settling, and long cooldowns can still be skipped immediately.
- Viper regression checks cover delayed updates, sequential follow-ups, persistent missing prerequisites, casting, the one-weave limit, and unsafe weave windows.
- The welcome screen describes the opener fix.

## 1.0.83.0

- Rotation settings apply to the running session without changing jobs or restarting.
- None, One and Two are input ceilings on every job. Viper no longer raises One to Two.
- None uses the normal global rotation instead of asking for pre-pull opener abilities.
- Settings changes clear held suggestions without forgetting weaves already used.
- Switching the opener off/on, or crossing into/out of globals-only mode, during combat leaves the normal rotation in charge until the next pull.
- A welcome screen explains changes after installation or an update. Once dismissed, it stays closed for that version. It waits until you are logged in and outside combat.
- Use `/otp changelog` to read the welcome screen again.
- A waving robot, peach and mint accents, and rounded update cards give the welcome screen a friendlier look. The robot is drawn directly in the UI, with no external image files.

One weave on Viper may leave some follow-up attacks unused. Choose Two only if comfortable.
