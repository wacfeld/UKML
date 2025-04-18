# ULTRAKILL Mustn't Live

My take on the currently unreleased ULTRAKILL Must Die difficulty.
Work in progress.

## Usage

All changes are applied universally.
Therefore to play the new difficulty, simply select Brutal difficulty with the mod installed.

## Current features

All changes are relative to Brutal difficulty

<details>
<summary>General changes</summary>

- Hard damage multiplier set to 50%
- Non-homing projectile speed doubled
- Homing projectile turning speed doubled
- Enemy projectiles (including Schism beams) won't damage enemies unless parried or redirected by explosions
- Enemy-caused explosions won't directly damage enemies (but can still light susceptible enemies on fire)
</details>

<details>
<summary>Malicious Face changes</summary>

- Projectile cooldown and beam charge made much faster
- Enrage when covered in gasoline
- Enragement fixed to work independent of health
- Every other beam attack is unparryable
- Beam attack parry window lowered from 0.5s to 0.25s
</details>

<details>
<summary>Cerberus changes</summary>

- Orb projectiles now bounce several times, creating a shockwave and explosion with each bounce
- Orb projectiles are no longer affected by enemy-caused explosions
- Cerberi enrage when another Cerberus reaches half health
- Stomps gain an additional vertical shockwave
- Dashes gain an alternating diagonal shockwave
- Enraged Cerberus gets an additional dash which creates a horizontal shockwave
- Inter-dash cooldown is either 0.25s (75% chance) or 0.75s (25% chance)
  - Enraged cooldown is always 0.25s
- Increased attack cooldown rate (2x for unenraged, 4x for enraged)
- Increased animation speed (1.2x for unenraged, 1.5x for enraged)
</details>

## Manual Installation

- Install [BepInEx](https://thunderstore.io/c/ultrakill/p/BepInEx/BepInExPack/) if you don't have it already
- Go to the latest [Release](https://github.com/wacfeld/UKML/releases)
- Download the dll and move it to `ULTRAKILL/BepInEx/plugins`
