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
    * [Cartel Gatherings](#cartel-gatherings)
    * [Business Sabotage](#business-sabotage)
* [End Game Quests](#end-game-quests)
    * [Unexpected Alliances](#unexpected-alliances)
    * [Infiltrate Manor](#infiltrate-manor)
    * [Four Wheels](#four-wheels) 
* [Modifying Cartel Dealers](#modifying-cartel-dealers)
* [Modifying Ambush Events](#modifying-ambush)
    * [Modifying Ambush Spawns](#modifying-default-spawns)
    * [Modifying Default Spawns](#modifying-default-spawns)
    * [Modifying Ambush Settings](#modifying-ambush-settings)
* [Modifying Cartel Stolen Items](#modifying-cartel-stolen-items)
* [Modifying Influence Changing Events](#modifying-influence-changing-events)
* [Debug Mode](#debug-mode)
* [In Multiplayer](#in-multiplayer)

---

### Features

- Adds more Cartel Ambush locations from the **ambush.json** file.
- Allows configuration of default ambush locations, spawns, and radius from the **default.json** file.
- Allows configuration of influence rewarded by certain events from the **influence.json** file
- **Realistic Robberies:** Overhauled dealer robberies where a robber actually spawns to fight the dealer. Defend the dealer to reclaim stolen items and money, or chase down the escaping robber.
- **Drive-By Events:** Experience drive-by attacks by Thomas in designated areas when the Cartel is hostile.
- **Mini-Quests:** Take on missions from select NPCs to find Cartel dead drops and weaken their regional influence.
- **End Game Quests:** 3 New Quests where you get to fight enforced cartel members and weaken their influence across the entire Hyland Point.
- **Intercept Deals:** A new event where a Cartel Dealer attempts to intercept player deals and additionally sends Cartel Dealers to deal more often.
- **Cartel Gatherings:** Group of 3 Cartel Goons will spawn during day time at random locations to gather and chill. Killing Cartel Dealers will make the gatherings hostile. Gatherings frequency and hostility is dynamic based on the amount of Cartel Dealers killed. Gatherings will only use unlocked regions and they unlock new locations with player progression.
- **Business Sabotage:** Cartel will try to actively interfere with your laundering activities at Post Office, Laundromat and Taco Ticklers. Defuse the planted explosive before your business blows up!
- **Enhanced Cartel Dealers:** Cartel dealers provide additional challenge and compete with deals with you and your dealers. They will try to intercept pending deal requests and dealers active deals. Cartel Dealers can be configured from the **CartelEnforcer/Dealers/dealer.json** file.
- **Persistence for Stolen Items:** Stolen items are now saved per save file.
- **Debug Mode:** Visualize all locations, trigger events manually for testing.

---
### Installation

1. Install **Melon Loader** from a trusted source like the official [MelonWiki](https://melonwiki.xyz/).
2. With **Melon Loader** install version **0.7.0** or **0.7.2 nightly builds** for Schedule I (0.7.1 is incompatible in IL2CPP)
    - If you use "alternate" Beta version in Steam -> Game Properties -> Betas, then any 0.7.x version is compatible (Must download MONO)
3. Manually download the correct .zip file and unzip it.
4. Copy the **.dll file** and the **Cartel Enforcer** folder into your **Mods** folder.

#### Mods folder structure

```
[Game Root]
└── Mods/
    ├── CartelEnforcer.dll (or CartelEnforcer-IL2Cpp.dll)
    └── CartelEnforcer/
        ├── config.json
        ├── Ambush/
        |   ├── ambush.json
        |   ├── default.json
        |   └── settings.json
        ├── CartelItems/ 
        |   └── (Persistent save data gets generated here)
        ├── Dealers/
        |   └── dealer.json
        └── Influence/
            └── influence.json
```

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
    "driveByFrequency":  0.7,
    "driveByEnabled": true,
    "realRobberyEnabled": true,
    "defaultRobberyEnabled": true,
    "miniQuestsEnabled": true,
    "interceptDeals": true,
    "enhancedDealers": true,
    "cartelGatherings": true,
    "businessSabotage": true,
    "endGameQuest": true,
    "endGameQuestMonologueSpeed": 1.0
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
    - `-1.0`: Can happen at most once every 4 in-game days
- **`driveByFrequency`**: Adjusts the frequency of the Drive-By events.
    - `1.0`: Random cooldown can have values down to 1 in-game hour.
    - `0.0`: Random cooldown is around 2 in-game days.
    - `-1.0`: Random cooldown can have values up to 4 in-game days.
- **`driveByEnabled`**:
    - `true`: Enables drive-by events.
    - `false`: Disables drive-by events.
- **`realRobberyEnabled`**:
    - `true`: Enables the realistic robbery system.
    - `false`: Disables the realistic robbery system.
- **`defaultRobberyEnabled`**:
    - `true`: Enables the game default robberies where you only get a text message
    - `false`: Disables the game default text message based robberies
- **`miniQuestsEnabled`**:
    - `true`: Enables mini-quests.
    - `false`: Disables mini-quests.
- **`interceptDeals`**:
    - `true`: Enables the Intercept Deals event.
    - `false`: Disables the event.
- **`enhancedDealers`**:
    - `true`: Enables the Enhanced Cartel Dealers feature
    - `false`: Disables the feature.
- **`cartelGatherings`**:
    - `true`: Enables the Cartel Gatherings event
    - `false`: Disables the event.
- **`businessSabotage`**:
    - `true`: Enables the Business Sabotage event
    - `false`: Disables the event.
- **`endGameQuest`**:
    - `true`: Enables the generation of End Game Quest.
    - `false`: Disables the generation.
- **`endGameQuestMonologueSpeed`**:
    - `1.0`: Monologue messages are displayed for roughly 5 seconds
    - `0.0`: Monologue messages are displayed for roughly 10 seconds
---

## Events and Activities


<img src="https://i.imgur.com/9umAuV9.png">

### Realistic Robberies

When a dealer is being robbed, a robber will spawn and engage them in a fight. Your actions affect the regional Cartel influence:
- **Robber defeated:** If the robber is killed or knocked out before the dealer dies, regional influence decreases by 80. If the robber is knocked out while escaping, regional influence decreases by 50.
- **Player flees:** If you run out of range, regional influence increases by 80.
- **Successful escape:** If the robber kills the dealer and reaches a safehouse, regional influence increases by 80.
- **Timeout:** If the combat is timed out dealer defends the robbery.
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
    - If you complete the deal before the Cartel dealer does, regional influence decreases by 50 and your relationship with the customer increases slightly more.
    - If the Cartel dealer successfully intercepts the deal, regional influence increases by 50, and your relationship with the customer decreases slightly.


---

<img src="https://i.imgur.com/iwXBRTJ.gif">

### Drive-By Events

These events only happen when the Cartel is hostile.
- Only happens at Night Time from 22:30 to 04:00
- Thomas will spawn in a car and try to shoot the player.
- They are triggered when the player is near one of the 14 designated hotspots (common dealing locations, homes, and businesses).
- These events have a randomized cooldown of 1-96 in-game hours based on the `driveByFrequency` parameter.
- Their frequency can be adjusted with the `activityFrequency` parameter.

---

<img src="https://i.imgur.com/NMcosDO.png">

### Mini-Quests

Mini-quests can be obtained from select NPCs (Anna, Fiona, Dean, Mick, Jeff, Dan, Jeremy, Marco or Herbert).
- The quest-giving NPCs are chosen randomly every 4 ingame hours. Random Choice prefers Unlocked NPCs more.
- **Refusal Rate:** The chance an NPC will refuse to give you a quest is now based on your relationship with them. It ranges from a base 20% chance (at worst relations) to a base 50% chance (at best relations).
- **Time Window:** When asking the NPC for rumours during 12:00 to 18:00, the NPC has higher likelihood of giving the __Intercept Cartel Dead Drop__ quest.
- **Payment:** The cost to get a tip is dynamic, ranging from $100 (at best relations) to $500 (at worst relations).
- **Cartel Gathering Effect:** When asking for rumours during time which cartel is gathering, there is higher likelyhood of the NPC Revealing the __active Gathering Location__ instead of giving a quest.

#### Intercept Cartel Dead Drop

- **Dead Drop Location:** Based on the NPC relations there is 60% chance (at best relations) to tell exact location of the dead drop, and 40% chance to tell only the region. At worst relations there is 30% chance to tell the exact location and 70% chance to tell only the region.
- You have a 60 seconds to find the dead drop.
- **Success:** If you find the dead drop in time, you get +100 XP and the regional influence decreases by 25.
- **Failure:** If you fail to find it, the items vanish and regional influence increases by 50.
- **Loot Pools:** One of the following two pools is selected for each quest:
    - **Common (80% chance):** 3-10 items chosen from: Cocaine, Meth, Green Crack Seed, OG Kush Seed.
    - **Rare (20% chance):** 1 item chosen from: Silver Watch, Gold Watch, Silver Chain, Gold Chain, Old Man Jimmy's, Brut du Gloop.
    - **Stolen Items:** Mini-quest rewards now also include some of the items stolen by the Cartel. This is additional to the Loot Pool selected drop.

---

<img src="https://i.imgur.com/PYf7vk8.png">


### Cartel Gatherings

Cartel Gatherings are a regional task that appear frequently and even during the early game, before the cartel becomes hostile. Their locations are picked from all unlocked regions, with new locations unlocking as you progress through the game. 

You can reveal these gathering locations by bribing certain mini quest NPCs.

Gatherings include a "loot goblin" goon carrying stolen items and money if the cartel has stolen from your dealers. 

Goons have varied behaviors, including random animations and voice lines, and they occasionally rotate. 

If player gets nearby they will get annoyed and eventually turn hostile.
If you have killed enough Cartel Dealers, the Gatherings will be hostile on sight at a range depending on the Cartel Dealer Activity value.

- Gathering lasts for 3 hours after which the goons go away
- Killing a gathering will decrease regional cartel influence by 80.
- If a gathering is not defeated, regional influence will increase by 25 (up to a maximum of 400).

---

<img src="https://i.imgur.com/HpUB672.png">


### Business Sabotage

Player owned businesses can now be sabotaged by Cartel. For these events you will get a notification indicating which business is being sabotaged. After the notification, a goon will spawn and go plant a bomb at your business. Defuse the bomb or your current laundering operation will fail!

- A planted bomb will explode automatically after 2 in-game hours
- If the planted bomb explodes, regional cartel influence increases by 200 and the current business laundering operations fail.
- If the player defuses the bomb, regional cartel influence decreases by 150.
- If the player kills the goon that is trying to plant a bomb, regional cartel influence decreases by 50.

>The frequency of Business Sabotage events is linked directly to the total cartel influence across all regions. When the cartel total influence is lower, these events become more frequent!

## End Game Quests

All end game quests scale in difficulty based on the total cartel influence across all regions. Higher total influence will result in enemies having more HP and being overall more lethal and harder to kill. XP Rewards are also scaled based on the total cartel influence to compensate for difficulty.

---

<img src="https://i.imgur.com/UDb9giZ.png">

### Unexpected Alliances

The End Game Quest can be started by speaking to Manny (the Warehouse Fixer). This Quest can be completed only once per session.

> Note: The *Unexpected Alliances* Quest is in early phase development and is subject to change in content, difficulty and rewards.

- **Quest Prerequirements:**
    1. Cartel must be Hostile
    2. Player must have Suburbia region unlocked
    3. Player must be atleast Enforcer rank

- Upon paying the $5000 Bribe to Manny, you get a custom active quest:
    - First you must intercept cartel dead drops twice
    - Additionally you must stop a Cartel Gathering once
    - After completing the first step you must wait for Manny to arrange a meeting and send a text.
    - After player attends the meeting and finishes the dialogue they get the final quest step
    - Kill the Cartel Brute
        - If you run more than 70 units away from the Brute the Quest will fail
        - If the Brute runs more than 70 units away from its spawn position the Quest will fail
        - The Brute enters a Rage Stage when low on HP, starting to drink Cuke to regain health and having random sprint speed boosts

- **Quest Rewards:**
    - XP Based on Total Cartel Influence (850 - 1700 XP)
    - You get a Gold Watch, Gold Chain and shotgun shells from the Cartel Brute inventory
    - Cartel Brute has a ~33% chance to drop a Shotgun
    - Customer relationships increase by 5% for all unlocked customers
    - All unlocked regions have their Cartel Influence decreased by 25%
    - And lastly but most importantly: *Bragging Rights*

---

<img src="https://i.imgur.com/esO142K.png">

### Infiltrate Manor

The End Game Quest can be started by speaking to Ray between 18:15 and 19:00 when they are smoking a cigarette near the courthouse. This Quest can be completed only once per session.

> Note: The *Infiltrate Manor* Quest is in early phase development and is subject to change in content, difficulty and rewards.

- **Quest Prerequirements:**
    1. Cartel must be Hostile
    2. Player must have Suburbia region unlocked
    3. Player must be atleast Enforcer rank

- Upon paying the $2500 Bribe to Ray, you get a custom active quest:
    - First you must investigate the forest near Manor
    - After investigating, return to Ray to obtain more information
    - Wait for Night to arrive before breaking into Manor
    - Break in through the back door of Manor
    - Kill Manor Goons and steal their loot
    - Investigate the Manor Upstairs rooms and find Thomas' safe
    - Leave the Manor before police arrive

- **Quest Rewards:**
    - XP Based on Total Cartel Influence (600 - 1200 XP)
    - Ray will give you -15% discount from all properties and businesses until the game is exited
    - All unlocked regions have their Cartel Influence decreased by 15%
    - Thomas' safe is filled in order to up to 5 items max from the loot table below

| Item | Drop Chance | Quantity | Notes |
| :--- | :--- | :--- | :--- |
| M1911 Magazine | **100%** | 1 | This item is a guaranteed drop. |
| M1911 Pistol | **33.4%** | 1 | |
| Cocaine | **10%** | 12-20 |  |
| Gold Bar | **5%** | 1-3 | This drop prevents cash from spawning. |
| Cash | High Chance | $800-$4000 | Will spawn if a gold bar does not. The amount is rounded to the nearest $100. |
| Silver Watch | **20%** | 1 |  |
| Silver Chain | **20%** | 1 |  |
| Stolen Cartel Item | **80%** | 1 | Only spawns if the `cartelStolenItems` pool has items. |

---


---

<img src="https://i.imgur.com/sWndjjy.png">

### Four Wheels

The End Game Quest can be started by speaking to Cranky Frank between 16:00 and 18:00 when they are smoking a cigarette near the Northern Waterfront. This Quest can be completed only once per session.

> Note: The *Four Wheels* Quest is in early phase development and is subject to change in content, difficulty and rewards.

- **Quest Prerequirements:**
    1. Cartel must be Hostile
    2. Player must have unlocked atleast 3 customers from the Docks region
    3. Player must be atleast Hustler rank

- Upon paying the $3500 Bribe to Cranky Frank, you get a custom active quest:
    - You must first ask Jeremy for additional information after 21:00 at their house. Bribing them costs $6000.
    - After talking to Jeremy you must stop the Cartel from transporting cocaine in the Northern Waterfront.
    - After killing all the enemies you must escape before police arrive.

- **Quest Rewards:**
    - XP Based on Total Cartel Influence (300 - 600 XP)
    - You get Cocaine Bricks from the SUV trunk (available after all cartel enemies are killed)
    - Cocaine Bricks amount scales with Total Cartel Influence
    - All unlocked regions have their Cartel Influence decreased by 10%

---

## Modifying Configs and spawns


### Modifying Cartel Dealers
You can customize the Cartel Dealers' settings through the **dealer.json** file.

1. Open the **CartelEnforcer/Dealers** folder inside your **Mods** directory.
2. Open the **dealer.json** file. Its contents by default are:

```json
{
  "CartelDealerMoveSpeedMultiplier": 1.65,
  "CartelDealerHP": 200.0,
  "CartelDealerWeapon": "M1911",
  "StealDealerContractChance": 0.06,
  "StealPlayerPendingChance": 0.08,
  "DealerActivityDecreasePerKill": 0.10,
  "DealerActivityIncreasePerDay": 0.25,
  "SafetyThreshold": -0.85,
  "SafetyEnabled": true,
  "FreeTimeWalking": true
}
```

- **`CartelDealerMoveSpeedMultiplier`**: Adjusts the **movement speed** of Cartel Dealers (Range 0.1 - 3.0)
- **`CartelDealerHP`**: Sets the **total health points** for a Cartel dealer (Range 10.0 - 2000.0)
- **`CartelDealerWeapon`**: Specifies the **weapon** used by Cartel dealers. Supported values are: "M1911", "Knife" and "Shotgun".
- **`StealDealerContractChance`**: Controls the **probability** for the Cartel Dealer stealing Players hired dealers active contracts. (Range 0.0 - 1.0)
- **`StealPlayerPendingChance`**: Controls the **probability** for the Cartel Dealer stealing Players pending deal requests. (Range 0.0 - 1.0)
- **`DealerActivityDecreasePerKill`**: A decrease in dealer activity for **each kill** the player makes. (Range 0.0 - 1.0)
- **`DealerActivityIncreasePerDay`**: An increase in dealer activity for **each in-game day** that passes. (Range 0.0 - 1.0)
- **`SafetyThreshold`**: Defines the **minimum value** required for a dealers to stop leaving their houses. This will cause them to stay inside if too many dealers are killed. (Range -1.0 - 1.0)
- **`SafetyEnabled`**: When set to `true`, this enables the usage of **SafetyThreshold**.
- **`FreeTimeWalking`**: When `true`, dealers will **randomly walk around** when they are not performing an active task. When `false`, they will remain stationary at their apartment door when spawning. This also controls their walking during time which cartel is not hostile.


---

### Modifying Ambush

#### Modifying Ambush Spawns

You can add or modify custom ambush locations.

1. Open the `Mods/CartelEnforcer/Ambush/ambush.json` file.
2. Edit existing entries or add new ones. Each entry has:
    - **`mapRegion`**: A number from `0-5` for the region.
    - **`ambushPosition`**: The X, Y, and Z coordinates of the trigger area.
    - **`spawnPoints`**: At least four spawn points for enemies (only X and Z values matter for ambushes above ground, in Sewers you need the exact negative y value too).
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

#### Modifying Ambush Settings

1. Open `Mods/CartelEnforcer/Ambush/settings.json`.
2. You can modify the weapons used for ambush events, change the minimum rank required for ranged weapon usage in ambushes and also disable the ambushes that happen rarely after deals get completed by player.

3. The file content is by default:
```json
{
    "RangedWeaponAssetPaths": [
        "Avatar/Equippables/Revolver",
        "Avatar/Equippables/M1911",
        "Avatar/Equippables/PumpShotgun"
    ],
    "MeleeWeaponAssetPaths": [
        "Avatar/Equippables/Knife"
    ],
    "MinRankForRanged": 2,
    "AfterDealAmbushEnabled": true
}
```

- **RangedWeaponAssetPaths** & **MeleeWeaponAssetPaths**: Paths to the weapon assets that get used in Ambushes.
    - Note: Must be a valid path and string is case sensitive (example: Machete melee weapon can't currently be loaded with "Avatar/Equippables/Machete")
- **MinRankForRanged**: Player Rank requirement that indicates when goons start using ranged weapons in ambushes.
- **AfterDealAmbushesEnabled**: When true by default, after player completes a deal an ambush can happen instantly after. When disabled these stop happening and ambushes are only triggered by positional triggers.

---

#### Modifying Cartel Stolen Items

1. Open `Mods/CartelEnforcer/CartelItems/(organisation name).json`.
2. You can modify the values here and change quantity of items as you wish or add new ones. Make sure the item ID is always a valid id. You can also change cartel stolen balance.
3. If you want to reset the stolen items in the specific save, you can delete the file and it will get regenerated.

---

### Modifying Influence Changing Events

1. Open `Mods/CartelEnforcer/Influence/influence.json`.
2. Each type of event and its corresponding influence change is listed in the file. 
3. Positive values means that Cartel Influence Increases and Negative values mean that Cartel Influence Decreases.
4. Values are divided by a thousand, meaning that for example 0.050 corresponds to 50 increase in Cartel Influence.

Note: Due to the way which the mod handles some of the influence changes, sometimes the displayed influence change is __not__ displaying the correct change and can rarely display the change twice for the event

---

### Debug Mode

In debug mode, you can see various visual cues and use keybinds to test features.

> The Debug Mode does not log anything into console in version 1.4.0 and above for performance reasons. For Console Logs you need to build the dll file from source code using DEBUG configuration. See [GitHub BUILD.md](https://github.com/XOWithSauce/schedule-cartelenforcer/blob/main/.github/BUILD.md) for more info.

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
    - `Left CTRL + U`: Generate the Infiltrate Manor Quest dialogue option for Jeremy, without checking prerequirements.
    - `Left CTRL + P`: Instantly spawn a Cartel Gathering at a random location
    - `Left CTRL + N`: Start a Sabotage Event at nearest supported business

---

### In Multiplayer

For multiplayer to function correctly, all players must have the same `default.json`, `ambush.json` and `config.json` content.

Not all events and activities added by this mod support multiplayer fully and might have bugs on server clients.

---
> **Note:** The `config.json`, `default.json` and persistent cartel stolen items files will be created automatically in the `Mods/CartelEnforcer/` directory if they are missing.

---

Contribute, Build from Source or Verify Integrity -> [GitHub](https://github.com/XOWithSauce/schedule-cartelenforcer/)
