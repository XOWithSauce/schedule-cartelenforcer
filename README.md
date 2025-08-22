# Cartel Enforcer
**Requires Melon Loader**

This mod adds new Cartel Ambush locations to the game and gives you control over existing ones. Dealer Robbing is also overhauled and realistic, spawning the robber and moving stolen items to inventory before robber escapes to a safehouse! Kill the robber to reclaim stolen items and money!

---

### Features

- Adds more Cartel Ambush locations to the game from the **ambush.json** file.
- Allows configuration of Game Default Ambush locations, spawns, and radius from the **default.json** file.
- **Debug Mode** lets you visualize all locations and corresponding spawns.
- Dealer Robbing is now Realistic! If player is near a dealer while they are getting robbed, this will spawn a Robber next to the Dealer and try to fight them!
- Works in Multiplayer

---

### Installation

1.  Install **Melon Loader** from a trusted source like the official [MelonWiki](https://melonwiki.xyz/).
2.  Manually download the correct .zip file and then unzip the file.
3.  Copy the **.dll file** and the **Cartel Enforcer** folder into the **Mods** folder.

---

### Configuration

You can customize the mod's settings through the **config.json** file.

1.  Open the **CartelEnforcer** folder inside your **Mods** directory.
2.  Open the **config.json** file. Its contents by default are:
```json
{
  "debugMode": true
}
```

-   **`debugMode`**:
    -   `true`: By default, show debug messages, visualize spawn locations, and display coordinates.
    -   `false`: Does not show the debug content. The cartel ambushes will still be added.


Example of Debug Mode, see Top Left for Coordinates display.

<img src="https://i.imgur.com/xEt43yQ.png">


**Cubes**:  Visualize ambushPosition. Cube size is the same as detectionRadius. Cube color is based on Region. When player stands under the cube, ambush will trigger eventually.

**Cyan Beams**: Visualize spawnPoints (4 of them for each Cube)

<img src="https://i.imgur.com/7x5l97m.png">

---


### In Multiplayer

While using the mod in multiplayer, all players must have the same default.json and ambush.json content.

---

### How to Make New Spawns

You can add your own custom ambush locations or modify existing ones.

1.  Open the **CartelEnforcer** folder, then the **Ambush** folder, and locate the **ambush.json** file.
2.  Each mod-added ambush is listed here and can be edited.
    -   **`mapRegion`**: A number from `0-5` depending on which region the ambush belongs to.
    -   **`ambushPosition`**: The X, Y, and Z coordinates of the area that triggers the ambush.
    -   **`spawnPoints`**: There must be at least four spawn points. Only the X and Z values matter.
    -   **`detectionRadius`**: A decimal number indicating how close the player must be to the `ambushPosition`.

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

### Modifying Default Spawns

1.  Open the **CartelEnforcer** folder, then the **Ambush** folder, and locate the **default.json** file.
2.  Each default game ambush is listed here and can be edited. **Do not add or remove** any ambushes to this file, but **only modify** the values.
3.  Note that every time game gets a new **update**, you should **delete default.json**. It will get created again when you load a save. This way you are "updating" your default values too in the json config.
---
### Dealer Robbing Overhaul

#### How It Works:

* **Triggering the Encounter:** When you are within 60 units of a dealer about to be robbed, a message is sent asking for help, and a robber spawns to start a fight.

* **Combat Outcomes:**
    * **Successful Defense:** The robbery is successfully defended if the robber dies, is knocked out, or if the fight lasts longer than 60 seconds.
    * **Player Cowardice:** If you flee more than 90 units from the robber, the dealer will defend the robbery on their own.

* **Robber's Escape:**
    * If the dealer is defeated, the robber enters the escape phase, gaining a temporary **Adrenaline Boost** (speed and minor health regen) and stealing items.
    * The robber attempts to flee to the nearest Cartel safehouse, ignoring all combat.
    * **Chase Down:** If you defeat the escaping robber, you can reclaim the stolen items.
    * **Failed Escape:** If a safehouse is not found, the robber will flee from the player for 60 seconds before despawning.

---

> **Note:** The **config.json** and **default.json** files will get created automatically in the `Mods/CartelEnforcer/` directory if they are missing.

---
