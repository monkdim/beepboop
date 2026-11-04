# Changes

## 1.0.87.0

- Ninja's area button stops botching its own mudras. The opening mudra is forced to Chi by the button, but the second one read the enemy count on its own, so below three targets it answered Chi again and the game turned the pair into Rabbit Medium - every ninjutsu the area button started, which is most of a dungeon pull once the pack thins. Whichever button opened the sequence now finishes it, and the single-target button is guarded the same way against a pack gathering mid-sequence.
- Bard holds a song for its full run instead of burning two in one weave window. All three cooldowns are independent, so the moment two were up the list offered one and then the other six tenths of a second later; a recorded pull spent Mage's Ballad for a single global and then had no song at all from 01:07 to 02:02. The song that is playing now decides the hand-off, at the 3 seconds The Balance gives. Army's Paeon should be cut shorter still, at 12-10, and is not yet: that needs to know which song the gauge's id stands for, so the recorded log now prints it.
- Bard's recorded log carries a gauge line: song id and timer, repertoire, soul voice and coda.
- An opener weave whose own recast is still turning is stepped over however little is left on it. The same Bard pull read "step 16 of 21, gave up: step 16 (Empyreal Arrow) was not usable (#582)" and threw away six steps over about 1.6 seconds.
- Gunbreaker puts Double Down and Sonic Break ahead of starting a fresh Gnashing Fang. Gnashing Fang only needed a cartridge to exist, which is true on nearly every global in No Mercy, so it took both charges and six of the eight globals in the first window while the thousand-potency Double Down sat unused and Sonic Break landed 3.4 seconds after the window closed. In filler it now waits for a capped charge or for No Mercy within 7 seconds.
- Gunbreaker has The Balance's opener. There was none, and the list opened with No Mercy on the second global against an empty magazine.
- Trajectory is no longer offered as damage. It has no potency line at all - it is the gap closer - and it was spending four of the most contested weave slots in the rotation, adding enmity and dragging the tank onto the boss each time.

## 1.0.86.0

- Summoner's two buttons follow demi-summon and primal phases, including elemental attacks and Ifrit's follow-ups.
- White Mage, Scholar, Astrologian and Sage now have single-target and AoE damage buttons.
- Astrologian's two buttons draw and play targeted cards with role-based recipients and eligible backups. Manual healing and card buttons are unchanged.

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
