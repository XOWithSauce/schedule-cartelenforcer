
# Cartel Enforcer
**Requires Melon Loader**

Cartel Enforcer adds new features and challenges to the Cartel, including new ambush locations, events, and a better dealer robbery system. Experience the drive-by events, take on mini-quests, and influence the enforced Cartel's presence in new ways!

---

### Table of Contents
* [Features](#features)
* [Installation](#installation)
* [Configuration](#configuration)
* [Debug Mode](#debug-mode)
* [In Multiplayer](#in-multiplayer)
* [Events and Activities](#events-and-activities)
    * [Realistic Robberies](#realistic-robberies)
    * [Drive-By Events](#drive-by-events)
    * [Mini-Quests](#mini-quests)
* [Modifying Spawns](#modifying-spawns)
    * [Modifying Default Spawns](#modifying-default-spawns)

---

### Features

- Adds more Cartel Ambush locations from the **ambush.json** file.
- Allows configuration of default ambush locations, spawns, and radius from the **default.json** file.
- **Realistic Robberies:** Overhauled dealer robberies where a robber actually spawns to fight the dealer. Defend the dealer to reclaim stolen items and money, or chase down the escaping robber.
- **Drive-By Events:** Experience drive-by attacks by Thomas in designated areas when the Cartel is hostile.
- **Mini-Quests:** Take on missions from select NPCs to find Cartel dead drops and weaken their regional influence.
- **Debug Mode:** Visualize all locations, debug messages, and trigger events manually for testing.

---
### Installation

1. Install **Melon Loader** from a trusted source like the official [MelonWiki](https://melonwiki.xyz/).
2. Manually download the correct .zip file and unzip it.
3. Copy the **.dll file** and the **Cartel Enforcer** folder into your **Mods** folder.

---

### Configuration

You can customize the mod's settings through the **config.json** file.

1. Open the **CartelEnforcer** folder inside your **Mods** directory.
2. Open the **config.json** file. Its contents by default are:

```json
{
    "debugMode": false,
    "activityFrequency": 0.0,
    "activityInfluenceMin": 0.0,
    "driveByEnabled": true,
    "realRobberyEnabled": true,
    "miniQuestsEnabled": true
}
```

- **`debugMode`**:
    - `true`: Show debug messages, visualize spawn locations, and display coordinates.
    - `false`: Hides debug content. The cartel features will still be active.
- **`activityFrequency`**: Adjusts how often Cartel activities occur.
    - `1.0`: Activities happen roughly 10 times more frequently.
    - `-1.0`: Activities happen roughly 10 times less frequently.
    - `0.0` (Default): Activities happen at the game's default frequency.
- **`activityInfluenceMin`**: Changes the minimum Cartel Influence required for activities.
    - `1.0`: Activities will rarely happen, as they require maximum regional influence.
    - `-1.0`: Activities do not require any regional influence and can happen anywhere.
    - `0.0` (Default): Influence requirements are set to the game's default.
- **`driveByEnabled`**:
    - `true`: Enables drive-by events.
    - `false`: Disables drive-by events.
- **`realRobberyEnabled`**:
    - `true`: Enables the realistic robbery system.
    - `false`: Disables the realistic robbery system.
- **`miniQuestsEnabled`**:
    - `true`: Enables mini-quests.
    - `false`: Disables mini-quests.

---

### Debug Mode

In debug mode, you can see various visual cues and use keybinds to test features:

- **Cubes**: Visualize ambush positions. Their size corresponds to the detection radius and their color is based on the region. Standing under a cube will eventually trigger an ambush.
- **Cyan Beams**: Visualize the four spawn points for each cube.
- **Orange Spheres**: Show the locations of drive-by events triggers. Standing under the Sphere will eventually trigger a Drive-By.

<img src="https://i.imgur.com/xEt43yQ.png">

<img src="https://i.imgur.com/7x5l97m.png">





- **Keybinds:**
    - `Left CTRL + R`: Trigger a Dealer Robbery at the nearest dealer.
    - `Left CTRL + G`: Trigger an instant drive-by at the nearest location.
    - `Left CTRL + H`: Give a mini-quest to one of the select NPCs.

---

### In Multiplayer

For multiplayer to function correctly, all players must have the same `default.json`, `ambush.json` and `config.json` content.

---

### Events and Activities

#### Realistic Robberies

When a dealer is being robbed, a robber will spawn and engage them in a fight. Your actions affect the regional Cartel influence:
- **Robber defeated:** If the robber is killed or knocked out, regional influence decreases by 80.
- **Player flees:** If you run out of range, regional influence increases by 20.
- **Successful escape:** If the robber kills the dealer and reaches a safehouse, regional influence increases by 50.

#### Drive-By Events

These events only happen when the Cartel is hostile.
- Only happens at Night Time from 22:30 to 04:00
- Thomas will spawn in a car and try to shoot the player.
- They are triggered when the player is near one of the 11 designated hotspots (common dealing locations, homes, and businesses).
- These events have a randomized cooldown of 16-48 in-game hours.
- Their frequency can be adjusted with the `activityFrequency` parameter.

#### Mini-Quests

Mini-quests can be obtained from select NPCs (Anna, Fiona, Dean, Mick, or Jeff).
- The quest-giving NPCs are chosen randomly every 8-16 hours.
- There's a 70% chance an NPC will refuse to give you a quest.
- If they agree, you pay a $100 bribe to get a tip on a Cartel dead drop.
- **Dead Drop Location:** The NPC has an 80% chance to only reveal the region and a 20% chance to tell you the exact location.
- You have a limited time (30-120 seconds) to find the dead drop.
- **Success:** If you find the dead drop in time, you get +100 XP and the regional influence decreases by 25.
- **Failure:** If you fail to find it, the items vanish and regional influence increases by 50.
- **Loot Pools:** One of the following two pools is selected for each quest:
    - **Common (80% chance):** 3-10 items chosen from: Cocaine, Meth, Green Crack Seed, OG Kush Seed.
    - **Rare (20% chance):** 1 item chosen from: Silver Watch, Gold Watch, Silver Chain, Gold Chain, Old Man Jimmy's, Brut du Gloop.

---

### Modifying Spawns

You can add or modify custom ambush locations.

1. Open the `Mods/CartelEnforcer/Ambush/ambush.json` file.
2. Edit existing entries or add new ones. Each entry has:
    - **`mapRegion`**: A number from `0-5` for the region.
    - **`ambushPosition`**: The X, Y, and Z coordinates of the trigger area.
    - **`spawnPoints`**: At least four spawn points for enemies (only X and Z values matter).
    - **`detectionRadius`**: A decimal number for how close the player must be to the `ambushPosition`.

```json
    {
      "mapRegion": 5,
      "ambushPosition": {
        "x": 143.05,
        "y": 1.75,
        "z": -16.73
      },
      "spawnPoints": [
        {
          "x": 138.56,
          "y": 0.0,
          "z": -38.61
        },
        {
          "x": 128.19,
          "y": 0.0,
          "z": -9.84
        },
        {
          "x": 135.87,
          "y": 0.0,
          "z": -6.18
        },
        {
          "x": 155.29,
          "y": 0.0,
          "z": -23.90
        }
      ],
      "detectionRadius": 10.0
    },
```

#### Modifying Default Spawns

1. Open `Mods/CartelEnforcer/Ambush/default.json`.
2. You can only **modify** the values here; do not add or remove any ambushes.
3. If the game receives a new update, delete `default.json` to ensure your configuration is up to date. It will be recreated the next time you load a save.

---
> **Note:** The `config.json` and `default.json` files will be created automatically in the `Mods/CartelEnforcer/` directory if they are missing.

---

Contribute, Build from Source or Verify Integrity -> [GitHub](https://github.com/XOWithSauce/schedule-cartelenforcer/)


Finance the development or support my creations -> [Ko-fi](https://ko-fi.com/dahjp)