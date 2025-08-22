# Cartel Enforcer
**Requires Melon Loader**

This mod adds new Cartel Ambush locations to the game and gives you control over existing ones.

---

### Features

- Adds more Cartel Ambush locations to the game from the **ambush.json** file.
- Allows configuration of Game Default Ambush locations, spawns, and radius from the **default.json** file.
- **Debug Mode** lets you visualize all locations and corresponding spawns.

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

<img src="https://i.imgur.com/xEt43yQ.png">

<img src="https://i.imgur.com/7x5l97m.png">


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

> **Note:** The **config.json** and **default.json** files will get created automatically in the `Mods/CartelEnforcer/` directory if they are missing.

---
