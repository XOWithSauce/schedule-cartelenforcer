# Cartel Enforcer
**Requires Melon Loader**

Cartel Enforcer adds new features and challenges to the Cartel, including new ambush locations, events, and a better dealer robbery system. Experience the drive-by events, take on mini-quests, and influence the enforced Cartel's presence in new ways!

---

### Table of Contents
* [Features](#features)
* [Installation](#installation)
* [Configuration](#configuration)
* [Events and Activities](#events-and-activities)
    * [Realistic Robberies](#realistic-robberies)
    * [Intercept Deals](#intercept-deals)
    * [Drive-By Events](#drive-by-events)
    * [Mini-Quests](#mini-quests)
    * [End Game Quests](#end-game-quests)
* [Debug Mode](#debug-mode)
* [In Multiplayer](#in-multiplayer)
* [Modifying Spawns](#modifying-spawns)
    * [Modifying Default Spawns](#modifying-default-spawns)

---

### Features

- Adds more Cartel Ambush locations from the **ambush.json** file.
- Allows configuration of default ambush locations, spawns, and radius from the **default.json** file.
- **Realistic Robberies:** Overhauled dealer robberies where a robber actually spawns to fight the dealer. Defend the dealer to reclaim stolen items and money, or chase down the escaping robber.
- **Drive-By Events:** Experience drive-by attacks by Thomas in designated areas when the Cartel is hostile.
- **Mini-Quests:** Take on missions from select NPCs to find Cartel dead drops and weaken their regional influence.
- **End Game Quests:** 2 New Quests where you get to fight enforced cartel members and weaken their influence across the entire Hyland Point.
- **Intercept Deals:** A new event where a Cartel Dealer attempts to intercept player deals and additionally sends Cartel Dealers to deal more often.
- **Persistence for Stolen Items:** Stolen items are now saved per save file.
- **Debug Mode:** Visualize all locations, trigger events manually for testing.

---
### Installation

1. Install **Melon Loader** from a trusted source like the official [MelonWiki](https://melonwiki.xyz/).
2. With **Melon Loader** install version **0.7.0** for Schedule I (0.7.1 is incompatible in IL2CPP)
3. Manually download the correct .zip file and unzip it.
4. Copy the **.dll file** and the **Cartel Enforcer** folder into your **Mods** folder.

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
    "ambushFrequency": 1.0,
    "deadDropStealFrequency": 1.0,
    "cartelCustomerDealFrequency": 1.0,
    "cartelRobberyFrequency": 1.0,
    "cartelDealChance": 0.1,
    "driveByEnabled": true,
    "realRobberyEnabled": true,
    "miniQuestsEnabled": true,
    "interceptDeals": true,
    "endGameQuest": true
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
- **`ambushFrequency`**: Adjusts the frequency of Ambush events.
    - `1.0`: Ambush can happen as often as every 1 in-game hour.
    - `0.0`: Ambush can happen at most once every 2 in-game days.
    - `-1.0`: Ambush can happen at most once every 4 in-game days.
- **`deadDropStealFrequency`**: Adjusts the frequency of Dead Drop Steal events.
    - `1.0`: Can happen as often as every 1 in-game hour.
    - `0.0`: Can happen at most once every 2 in-game days.
    - `-1.0`: Can happen at most once every 4 in-game days.
- **`cartelCustomerDealFrequency`**: Adjusts the frequency of Cartel Customer Deal events.
    - `1.0`: Can happen as often as every 1 in-game hour.
    - `0.0`: Can happen at most once every 2 in-game days.
    - `-1.0`: Can happen at most once every 4 in-game days.
- **`cartelRobberyFrequency`**: Adjusts the frequency of Cartel Robbery events.
    - `1.0`: Can happen as often as every 1 in-game hour.
    - `0.0`: Can happen at most once every 2 in-game days.
    - `-1.0`: Can happen at most once every 4 in-game days.
- **`cartelDealChance`**: Adjusts the likelihood of Cartel Dealers going out and making extra deals.
    - `1.0`: Cartel Dealers will have 100% chance of going out to an extra deal.
    - `0.0`: Cartel Dealers will have 0% chance of going out to an extra deal.
- **`driveByEnabled`**:
    - `true`: Enables drive-by events.
    - `false`: Disables drive-by events.
- **`realRobberyEnabled`**:
    - `true`: Enables the realistic robbery system.
    - `false`: Disables the realistic robbery system.
- **`miniQuestsEnabled`**:
    - `true`: Enables mini-quests.
    - `false`: Disables mini-quests.
- **`interceptDeals`**:
    - `true`: Enables the Intercept Deals event.
    - `false`: Disables the event.
- **`endGameQuest`**:
    - `true`: Enables the generation of End Game Quest.
    - `false`: Disables the generation.
---

### Events and Activities

---

<img src="https://i.imgur.com/9umAuV9.png">

### Realistic Robberies

When a dealer is being robbed, a robber will spawn and engage them in a fight. Your actions affect the regional Cartel influence:
- **Robber defeated:** If the robber is killed or knocked out before the dealer dies, regional influence decreases by 80. If the robber is knocked out while escaping, regional influence decreases by 25.
- **Player flees:** If you run out of range, regional influence increases by 20.
- **Successful escape:** If the robber kills the dealer and reaches a safehouse, regional influence increases by 50.

---

<img src="https://i.imgur.com/xJzpiAK.png">

### Intercept Deals

This is a new type of event where the Cartel actively attempts to intercept one of your deals.
- The event can only occur between 16:20 and 04:20 and when the Cartel is hostile.
- Randomized Frequency of Intercept Deals feature is tied to the Activity Frequency config value
- Only deals with less than 5 hours and more than 1 hour and 30 minutes remaining can be intercepted.
- If the player is within 40 units of the customer, the intercept is canceled.
- The Cartel Dealer can have your Stolen Items in their inventory
- **Event Timeline:**
    - When the event starts, the quest icon on the left side of the screen changes to the Benzies logo.
    - A timer of 30 seconds begins before the Cartel dealer starts their intercept.
- **Outcomes:**
    - If you complete the deal within the 30 second grace period, Cartel influence decreases by 100 and your relationship with the customer increases slightly more.
    - If you complete the deal after the grace period but before the Cartel dealer does, Cartel influence decreases by 100 and your relationship with the customer increases slightly more.
    - If the Cartel dealer successfully intercepts the deal, regional influence increases by 100, and your relationship with the customer decreases to the next tier below.

Additionally Intercept Deals Feature has second mechanism:
- Inside the time window 16:20 - 04:20 Cartel Dealers now have extra chance to make deals, based on the **cartelDealChance** value. These deals are not capped by the **cartelCustomerDealFrequency** and are additional to the base game behaviour.
- Every time an intercept is calculated (based on activity frequency), all of the Cartel Dealers will have a chance to intercept one players pending offers or one of your dealers active deals.


---

<img src="https://i.imgur.com/iwXBRTJ.gif">

### Drive-By Events

These events only happen when the Cartel is hostile.
- Only happens at Night Time from 22:30 to 04:00
- Thomas will spawn in a car and try to shoot the player.
- They are triggered when the player is near one of the 11 designated hotspots (common dealing locations, homes, and businesses).
- These events have a randomized cooldown of 16-48 in-game hours.
- Their frequency can be adjusted with the `activityFrequency` parameter.

---

<img src="https://i.imgur.com/NMcosDO.png">

### Mini-Quests

Mini-quests can be obtained from select NPCs (Anna, Fiona, Dean, Mick, or Jeff).
- The quest-giving NPCs are chosen randomly every 8-16 hours. Random Choice prefers Unlocked NPCs more.
- **Refusal Rate:** The chance an NPC will refuse to give you a quest is now based on your relationship with them. It ranges from a 40% chance (at best relations) to a 70% chance (at worst relations).
- **Time Window:** When asking the NPC for rumours during 12:00 to 18:00, the NPC has higher likelihood of giving the quest.
- **Payment:** The cost to get a tip is now dynamic, ranging from $100 (at best relations) to $500 (at worst relations).
- **Dead Drop Location:** Based on the NPC relations there is 60% chance (at best relations) to tell exact location of the dead drop, and 40% chance to tell only the region. At worst relations there is 30% chance to tell the exact location and 70% chance to tell only the region.
- You have a 60 seconds to find the dead drop.
- **Success:** If you find the dead drop in time, you get +100 XP and the regional influence decreases by 25.
- **Failure:** If you fail to find it, the items vanish and regional influence increases by 50.
- **Loot Pools:** One of the following two pools is selected for each quest:
    - **Common (80% chance):** 3-10 items chosen from: Cocaine, Meth, Green Crack Seed, OG Kush Seed.
    - **Rare (20% chance):** 1 item chosen from: Silver Watch, Gold Watch, Silver Chain, Gold Chain, Old Man Jimmy's, Brut du Gloop.
    - **Stolen Items:** Mini-quest rewards now also include some of the items stolen by the Cartel. This is additional to the Loot Pool selected drop.

---

## End Game Quests


<img src="https://i.imgur.com/UDb9giZ.png">

#### Unexpected Alliances

The End Game Quest can be started by speaking to Manny (the Warehouse Fixer). This Quest can be completed only once per session.

> Note: The *Unexpected Alliances* Quest is in early phase development and is subject to change in content, difficulty and rewards.

- **Quest Prerequirements:**
    1. Cartel must be Hostile
    2. Player must have atleast 5 customers unlocked from Suburbia Region
    3. Player must be atleast Enforcer rank

- Upon paying the $5000 Bribe to Manny, you get a custom active quest:
    - First you must intercept cartel dead drops twice (the Mini Quest in this document)
    - After intercepts, you must wait for Manny to arrange a meeting and send a text.
    - After player attends the meeting and finishes the dialogue they get the final quest step
    - Kill the Cartel Brute
        - If you run more than 70 units away from the Brute the Quest will fail
        - If the Brute runs more than 70 units away from its spawn position the Quest will fail
        - You have 2 minutes from when the fight starts to kill the Brute

- **Quest Rewards:**
    - 1000 XP 
    - You get a Gold Watch and Gold Chain from the Cartel Brute inventory
    - Customer relationships increase by 5% for all customers
    - All unlocked regions have their Cartel Influence decreased by 25%
    - And lastly but most importantly: *Bragging Rights*

---

<img src="https://i.imgur.com/esO142K.png">

### Infiltrate Manor

The End Game Quest can be started by speaking to Ray between 18:15 and 19:00 when they are smoking a cigarette near the bank. This Quest can be completed only once per session.

> Note: The *Infiltrate Manor* Quest is in early phase development and is subject to change in content, difficulty and rewards.

- **Quest Prerequirements:**
    1. Cartel must be Hostile
    2. Player must have atleast 5 customers unlocked from Suburbia Region
    3. Player must be atleast Enforcer rank

- Upon paying the $2500 Bribe to Ray, you get a custom active quest:
    - First you must investigate the forest near Manor
    - After investigating, return to Ray to obtain more information
    - Wait for Night to arrive before breaking into Manor
    - Break in through the back door of Manor
    - Kill Manor Goons and steal their loot
    - Investigate the Manor Upstairs rooms
    - Leave the Manor before police arrive

- **Quest Rewards:**
    - 850 XP 
    - You get money and rarely Silver Chains or Watches from Manor Goons
    - Ray will give you -15% discount from all properties and businesses until the game is exited
    - All unlocked regions have their Cartel Influence decreased by 15%

---

### Debug Mode

In debug mode, you can see various visual cues and use keybinds to test features.

> The Debug Mode does not log anything into console in version 1.4.0 and above for performance reasons. For Console Logs you need to build the dll file from source code using DEBUG configuration. See GitHub BUILD.md for more info.

- **Cubes**: Visualize ambush positions. Their size corresponds to the detection radius and their color is based on the region. Standing under a cube will eventually trigger an ambush.
- **Cyan Beams**: Visualize the four spawn points for each cube.
- **Orange Spheres**: Show the locations of drive-by events triggers. Standing under the Sphere will eventually trigger a Drive-By.

<img src="https://i.imgur.com/xEt43yQ.png">

<img src="https://i.imgur.com/7x5l97m.png">

- **Keybinds:**
    - `Left CTRL + R`: Trigger a Dealer Robbery at the nearest dealer.
    - `Left CTRL + G`: Trigger an instant drive-by at the nearest location.
    - `Left CTRL + H`: Give a mini-quest to one of the select NPCs.
    - `Left CTRL + L`: Log internal mod data to the console. ( Only Debug Builds )
    - `Left CTRL + T`: Trigger an Intercept Deal event.
    - `Left CTRL + Y`: Generate the Unexpected Alliances Quest dialogue option for Manny, without checking prerequirements.
    - `Left CTRL + U`: Generate the Infiltrate Manor Quest dialogue option for Manny, without checking prerequirements.

---

### In Multiplayer

For multiplayer to function correctly, all players must have the same `default.json`, `ambush.json` and `config.json` content.

Not all events and activities added by this mod support multiplayer fully and might have bugs on server clients.

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

#### Modifying Cartel Stolen Items

1. Open `Mods/CartelEnforcer/CartelItems/(organisation name).json`.
2. You can modify the values here and change quantity of items as you wish or add new ones. Make sure the item ID is always a valid id.
3. If you want to reset the stolen items in the specific save, you can delete the file and it will get regenerated.


---
> **Note:** The `config.json` and `default.json` files will be created automatically in the `Mods/CartelEnforcer/` directory if they are missing.

---

Contribute, Build from Source or Verify Integrity -> [GitHub](https://github.com/XOWithSauce/schedule-cartelenforcer/)


Finance the development or support my creations -> [Ko-fi](https://ko-fi.com/dahjp)
