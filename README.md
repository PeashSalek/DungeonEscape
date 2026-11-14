# Dungeon Escape: Curse of the Forgotten King

A 3D third-person action-adventure made in Unity 6 for the LUT University *Game Development Project* course.
Escape King Malgrath's cursed dungeon: fight skeletons, mages and brutes, pick locks, survive traps, choose cursed
blessings, trade with the Ghost Merchant and defeat the Forgotten King. Then test yourself in the endless **Trial of the King**.

## Play
Download the Windows build from the Releases / itch.io page, unzip and run `DungeonEscape.exe`.

### Controls (rebindable in Settings)
| Action | Key |
| --- | --- |
| Move | W A S D |
| Camera | Mouse |
| Sprint | Left Shift |
| Jump | Space |
| Dodge roll | Left Ctrl |
| Light attack (3-hit combo) | Left mouse |
| Heavy attack | Right mouse |
| Lock on | Middle mouse / Tab |
| Interact | E |
| Drink potion | Q |
| Pause | Esc |

## Features
- 3 story areas (Prison, Crypt, King's Chamber) + Trial of the King endless mode with a local top-10 high-score table
- Sword combat with combos, heavy attacks, dodge i-frames, stamina, lock-on
- Enemy AI state machines (patrol, chase, attack, search, return, stagger); melee, ranged and teleporting caster enemies
- Two-phase boss fight with summoned minions
- Keys, doors, lever gates, chests, a lock-picking minigame, breakable barrels, spike traps, poison water, swinging blades
- Curse shrines with random blessings (each with a curse), Ghost Merchant shop and coin economy, power-ups
- Brazier checkpoints with save/load, intro and ending cinematics with credits, pause menu, rebindable controls

## Building from source
1. Open the project in **Unity 6000.6.3f1**.
2. Add the player model (not included, see below).
3. Menu **Dungeon Escape → Build All Scenes** regenerates every scene from `Assets/Editor/LevelMaps.cs`.
4. Menu **Dungeon Escape → Build Windows** makes the player build in `Builds/Windows`.

### Player model (Mixamo, not in this repo)
The knight is Mixamo's free **Paladin J Nordstrom** character. Adobe's terms don't allow sharing the raw file, so it is excluded.
To add it: sign in at [mixamo.com](https://www.mixamo.com), search *Paladin J Nordstrom*, download it as **FBX for Unity (.fbx), With Skin**,
and save it as `Assets/Characters/Knight/Paladin.fbx`. Build All Scenes then imports it and extracts its textures.
Without it the game still builds and uses a plain mannequin for the knight.

Level layouts are text maps: edit `LevelMaps.cs` and rebuild to change a level.

## Credits
See [CREDITS.md](CREDITS.md). Code was written with AI assistance (Claude by Anthropic), as stated in the credits and the GDD.
