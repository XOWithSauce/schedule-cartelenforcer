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
    * [Steal Back Customers](#steal-back-customers)
* [Allied Extensions](#allied-extensions)
    * [Persuade Cartel Dealers](#persuade-cartel-dealers)
    * [Greet the Gathering Goons](#greet-the-gathering-goons)
    * [Allied Intro](#allied-intro)
    * [Allied Supplies](#allied-supplies)
* [End Game Quests](#end-game-quests)
    * [Unexpected Alliances](#unexpected-alliances)
    * [Infiltrate Manor](#infiltrate-manor)
    * [Four Wheels](#four-wheels) 
* [Modifying Configs](#modifying-configs-and-spawns)
    * [Modifying Event Frequency](#modifying-event-frequency)
    * [Modifying Cartel Dealers](#modifying-cartel-dealers)
    * [Modifying Ambush Events](#modifying-ambush)
        * [Modifying Ambush Spawns](#modifying-default-spawns)
        * [Modifying Default Spawns](#modifying-default-spawns)
        * [Modifying Ambush Settings](#modifying-ambush-settings)
    * [Modifying Drive By Events](#modifying-drive-by-events)
    * [Modifying Cartel Stolen Items](#modifying-cartel-stolen-items)
    * [Modifying Influence Changing Events](#modifying-influence-changing-events)
    * [Modifying Allied Settings](#modifying-allied-settings)
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
- **Steal Back Customers:** While hostile with the Cartel your customers might be stolen back by the Cartel, requiring you to re-unlock them with free samples!
- **Cartel Gatherings:** Group of 3 Cartel Goons will spawn during day time at random locations to gather and chill. Killing Cartel Dealers will make the gatherings hostile. Gatherings frequency and hostility is dynamic based on the amount of Cartel Dealers killed. Gatherings will only use unlocked regions and they unlock new locations with player progression.
- **Business Sabotage:** Cartel will try to actively interfere with your laundering activities at Post Office, Laundromat and Taco Ticklers. Defuse the planted explosive before your business and customers blow up!
- **Enhanced Cartel Dealers:** Cartel dealers provide additional challenge and compete with deals with you and your dealers. They will try to intercept pending deal requests and dealers active deals. Cartel Dealers can be configured from the **Dealers/dealer.json** file.
- **Allied Extensions:** Allied extensions is a bundle of features that allow the player to progress and complete the game even while cartel is Truced. While enabled, the player can hire Cartel Dealers and collect supplies awarded by the cartel!
- **Persistence for Stolen Items:** Stolen items are now saved per save file.
- **Debug Mode:** Visualize all locations, trigger events manually for testing.

---
### Installation

> If you are using Thunderstore Mod Manager you can skip these steps and just install the mod through the manager and it will work.

### Manual installation: 

1. Install **Melon Loader** from a trusted source like the official [MelonWiki](https://melonwiki.xyz/) and follow their setup instructions.
2. With **Melon Loader** install version **0.7.0** or **0.7.2 nightly builds** for Schedule I (0.7.1 is incompatible in IL2CPP)
    - If you use "alternate" Beta version in Steam -> Game Properties -> Betas, then any 0.7.x version is compatible (Must download MONO)
3. Download the correct version and unzip the downloaded folder, here you will find the **Mods** folder containing the mod .dll file and **UserData** folder containing the mod data folder
4. Copy the contents **Mods** folder into the **Steam/steamapps/common/Schedule I/Mods** folder
5. Copy the contents of **UserData** folder into the **Steam/steamapps/common/Schedule I/UserData** folder


### Config Files Locations

If you install the mod manually you will find all the config files from the following directory:

`UserData/XO_WithSauce-CartelEnforcer/`

If you installed with Thunderstore Mod manager the config files will be in the following directory:

`UserData/XO_WithSauce-CartelEnforcer_MONO/XO_WithSauce-CartelEnforcer/`

OR

`UserData/XO_WithSauce-CartelEnforcer_IL2CPP/XO_WithSauce-CartelEnforcer/`


### Configuration

You can enable and disable mod features with the first **config.json** file

You can alternatively change these settings with the **UserData/MelonPreferences.cfg**
file OR the **Schedule I mod manager phone app**.

The mod supports "hot-reloading" at runtime for all of these.

Manual editing:
1. Open the **XO_WithSauce-CartelEnforcer** folder inside your **UserData** directory.
2. Open the first **config.json** file. Its contents by default are:

```json
{
    "debugMode": false,
    "driveByEnabled": true,
    "realRobberyEnabled": true,
    "defaultRobberyEnabled": true,
    "miniQuestsEnabled": true,
    "interceptDeals": true,
    "enhancedDealers": true,
    "cartelGatherings": true,
    "businessSabotage": true,
    "stealBackCustomers": true,
    "alliedExtensions": true,
    "endGameQuest": true,
    "endGameQuestMonologueSpeed": 1.0
}
```

- **`debugMode`**:
    - `true`: Show debug messages, visualize spawn locations, and display coordinates.
    - `false`: Hides debug content. The cartel features will still be active.
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
- **`stealBackCustomers`**:
    - `true`: Enables the Steal Back Customers feature
    - `false`: Disables the feature.
- **`alliedExtensions`**:
    - `true`: Enables usage of Allied Extensions features. (Requires `endGameQuest` to be `true`)
    - `false`: Disables the features.
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
- **Player flees:** If you run out of range, regional influence increases by 25.
- **Successful escape:** If the robber kills the dealer and reaches a safehouse, regional influence increases by 25.
- **Timeout:** If the combat is timed out dealer defends the robbery.
---

<img src="https://i.imgur.com/xJzpiAK.png">

### Intercept Deals

This is a new type of event where the Cartel actively attempts to intercept one of your deals.
- The event can only occur between 16:20 and 04:20 and when the Cartel is hostile.
- You can change the frequency and add influence requirement from the `XO_WithSauce-CartelEnforcer/EventFrequency/config.json` file
- Only deals with less than 5 hours and more than 1 hour and 30 minutes remaining can be intercepted.
- If the player is within 40 units of the customer, the intercept is canceled.
- The Cartel Dealer can have your Stolen Items in their inventory
- **Event Timeline:**
    - When the event starts, the quest icon on the left side of the screen changes to the Benzies logo.
    - A timer of 30 seconds begins before the Cartel dealer starts their intercept.
- **Outcomes:**
    - If you complete the deal before the Cartel dealer does, regional influence decreases by 50 and your relationship with the customer increases slightly more.
    - If the Cartel dealer successfully intercepts the deal, regional influence increases by 25 and your relationship with the customer decreases slightly.


---

<img src="https://i.imgur.com/iwXBRTJ.gif">

### Drive-By Events

These events only happen when the Cartel is hostile.
- Only happens at Night Time from 22:30 to 04:00
- Thomas will spawn in a car and try to shoot the player.
- They are triggered when the player is near one of the 14 designated hotspots (common dealing locations, homes, and businesses).
- The event cooldown and influence requirement can be customized from the `XO_WithSauce-CartelEnforcer/EventFrequency/config.json` file
- All of the Drive By Triggers and drive routes can be customized from the `XO_WithSauce-CartelEnforcer/DriveBy/driveby.json` file

---

<img src="https://i.imgur.com/NMcosDO.png">

### Mini-Quests

Mini-quests can be obtained from select NPCs (Anna, Fiona, Dean, Mick, Jeff, Dan, Marco or Herbert).
- The quest-giving NPCs are chosen randomly every 4 ingame hours. Random Choice prefers Unlocked NPCs more.
- **Refusal Rate:** The chance an NPC will refuse to give you a quest is now based on your relationship with them. It ranges from a base 40% chance (at worst relations) to a base 75% chance (at best relations).
- **Time Window:** When asking the NPC for rumours during 12:00 to 18:00, the NPC has higher likelihood of giving the __Intercept Cartel Dead Drop__ quest.
- **Payment:** The cost to get a tip is dynamic, ranging from $100 (at best relations) to $500 (at worst relations).
- **Cartel Gathering Effect:** When asking for rumours during time which cartel is gathering, there is higher likelihood of the NPC Revealing the __active Gathering Location__ instead of revealing a Dead Drop location.

#### Intercept Cartel Dead Drop

- **Dead Drop Location:** Based on the NPC relations there is 75% chance (at best relations) to tell exact location of the dead drop, and 25% chance to tell only the region. At worst relations there is 25% chance to tell the exact location and 75% chance to tell only the region.
- You have a 60 seconds to find the dead drop if the exact location is revealed and 120 seconds if you only know the region.
- **Success:** If you find the dead drop in time, you get +100 XP and the regional influence decreases by 50.
- **Failure:** If you fail to find it, the items vanish and regional influence increases by 25.
- **Loot Pools:** One of the following two pools is selected for each quest:
    - **Common (80% chance):** 3-10 items chosen from: Cocaine, Meth, Green Crack Seed, OG Kush Seed.
    - **Rare (20% chance):** 1 item chosen from: Sewer Key, Silver Watch, Gold Watch, Silver Chain, Gold Chain, Old Man Jimmy's, Brut du Gloop.
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
- If the player has a Truce with the cartel then greeting all the gathering goons lowers influence by 150 and the Gathering will not be hostile or annoyed
- The event cooldown and influence requirement can be customized from the `XO_WithSauce-CartelEnforcer/EventFrequency/config.json` file


---

<img src="https://i.imgur.com/HpUB672.png">


### Business Sabotage

Player owned businesses can now be sabotaged by Cartel. For these events you will get a notification indicating which business is being sabotaged. After the notification, a goon will spawn and go plant a bomb at your business. Defuse the bomb or your current laundering operation will fail!

- A planted bomb will explode automatically after 2 in-game hours
- If the planted bomb explodes, regional cartel influence increases by 200 and the current business laundering operations fail.
- If the player defuses the bomb, regional cartel influence decreases by 150.
- If the player kills the goon that is trying to plant a bomb, regional cartel influence decreases by 50.
- The event cooldown and influence requirement can be customized from the `XO_WithSauce-CartelEnforcer/EventFrequency/config.json` file

>The frequency of Business Sabotage events is linked directly to the total cartel influence across all regions. When the cartel total influence is lower, these events become more frequent!

---


### Steal Back Customers

Unlocked customers can now be stolen back by the Cartel forcing you to re-unlock your customers by giving them free samples. This event scales in difficulty and frequency depending on how low the Cartels influence is across all regions. The lower their influence is, the harder they fight back!

- The feature can be disabled in the `config.json` by setting `stealBackCustomer` to `false`
- Player must have atleast 18 customers in total unlocked for this feature to kick in
- The maximum amount of customers Cartel can steal each day is 2
- Customer stealing prefers recently unlocked customers more
- Customer must have all of its connections unlocked to be valid for stealing
- Customers that have been stolen are harder to re-unlock with free samples, with successive samples increasing probability of unlock.



---


## Allied Extensions

Allied Extensions consists of multiple features that are added to the game while the Cartel is Truced. These features aim to provide new means of reducing influence even while Cartel is Truced in order to progress and complete the game. Additionally the Allied Extensions allow the player to Persude the Cartel Dealers to work for them!

- Allied Extensions features and cooldowns can be modified from the `XO_WithSauce-CartelEnforcer/Allied/config.json` file

- The Allied Extensions saves persistent data and progression to the `XO_WithSauce-CartelEnforcer/Allied/QuestData` folder for each save

---
### Persuade Cartel Dealers

After choosing to Truce with the Cartel, each Cartel Dealer gets new Persuade Dialogue Options! Each of the choices offer different probability of success to persuade the Cartel Dealer. Once successfully persuaded, the player can hire the Cartel Dealer to work for them! Each persuasion attempt lowers the regions Cartel Influence!

- Cooldown time can be changed from the `XO_WithSauce-CartelEnforcer/Allied/config.json` file at `PersuadeCooldownMins`


**Choice 1:** *Clothing Similarity*
- Change your clothes to match the Cartel Dealers clothes to increase the probability of a succesful persuasion

**Choice 2:** *Overall Cartel Influence*
- Decrease the overall Cartel Influence to increase the probability of a succesful persuasion

**Choice 3:** *Threatening*
- Wield your biggest and most powerful weapon before talking to the Cartel Dealer to increase the probability of a succesful persuasion

**Choice 4:** *Spread Rumours*
- Each rumour you spread increases the probability of a succesful persuasion


After hiring a succesfully persuaded Cartel Dealer the regions Cartel Influence is set to 0 instantly.

---

### Greet the Gathering Goons

While a Cartel Goon Gathering is active, greeting all 3 goons in a short timeframe will award the player with influence reduction in the respective region.

- The influence reduction can be changed from `XO_WithSauce-CartelEnforcer/Influence/influence.json` by changing the `trucedGreetingChallenge` value

---

### Allied Intro

After player attends the meeting with Thomas and chooses Truce or loads into a save where Westville Cartel Dealer is not recruited and Cartel is Truced, the Allied Intro Quest will be automatically enabled.

> Completion XP: 200

- Find the Westville Cartel Dealer
- Persuade the Westville Cartel Dealer
- Hire the Westville Cartel Dealer

---
### Allied Supplies

When the Cartel is Truced an Allied Supplies Quest will periodically appear! Grab the Cartel supplies before they disappear.

> Completion XP: 300

- Cooldown time can be changed from the `XO_WithSauce-CartelEnforcer/Allied/config.json` file at `SupplyQuestCooldownHours`
- Starts at 08:00 whenever the cooldown time has been waited
- The quest begins by Thomas Benzies sending you a message containing information about the supply location
- Find the supply location and grab the supplies to complete the quest
- Remember: Dont mess with the Allied Guard!!!

**Supply Loot by type:**
- Blue Barrels:
    - Acid
    - Phosphorus
    - Gasoline

- White Van:
    - Full spectrum grow lights
    - Drying racks
    - Air pots


---


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
    3. Player must be atleast Bagman rank

- Upon paying the $5000 Bribe to Manny, you get a custom active quest:
    - First you must intercept cartel dead drops twice
    - OR you must stop a Cartel Gathering once
    - After completing the first step you must wait for Manny to arrange a meeting and send a text.
    - After player attends the meeting and finishes the dialogue they get the final quest step
    - Kill the Cartel Brute
        - If you run more than 70 units away from the Brute the Quest will fail
        - If the Brute runs more than 70 units away from its spawn position the Quest will fail
        - The Brute enters a Rage Stage when low on HP, starting to drink Cuke to regain health and having random sprint speed boosts

- **Quest Rewards:**
    - XP Based on Total Cartel Influence (850 - 1700 XP)
    - You get a Shotgun, Gold Watch, Gold Chain and shotgun shells from the Cartel Brute inventory
    - Customer relationships increase by 5% for all unlocked customers
    - Police Law Intensity is lowered by 25%
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
    3. Player must be atleast Bagman rank

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
| Gold Bar | **70%** | 3-7 | This drop prevents cash from spawning. |
| Cash | **30%** | $1000 | Will spawn if a gold bar does not.  |
| Silver Watch | **60%** | 1 |  |
| Silver Chain | **60%** | 1 |  |
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

### Modifying Event Frequency

> Cooldowns of specified events are saved for each save separately into the XO_WithSauce-CartelEnforcer/EventFrequency/Cooldowns folder

You can customize the frequency of mod added events and also the base game cartel events from the Event Frequency config.

In this file you can also change influence requirements for all events.

1. Open the **XO_WithSauce-CartelEnforcer/EventFrequency** folder inside your **UserData** directory.
2. Open the **config.json** file. Its contents by default are:

```json
{
  "events": [
    {
      "Identifier": "Ambush",
      "CooldownHours": 0,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    },
    {
      "Identifier": "RegionActivity",
      "CooldownHours": 0,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    },
    {
      "Identifier": "StealDeadDrop",
      "CooldownHours": 0,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    },
    {
      "Identifier": "CartelCustomerDeal",
      "CooldownHours": 0,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    },
    {
      "Identifier": "RobDealer",
      "CooldownHours": 0,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    },
    {
      "Identifier": "SprayGraffiti",
      "CooldownHours": 0,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    },
    {
      "Identifier": "CartelPlayerDeal",
      "CooldownHours": 0,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    },
    {
      "Identifier": "DriveBy",
      "CooldownHours": 0,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    },
    {
      "Identifier": "InterceptDeals",
      "CooldownHours": 0,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    },
    {
      "Identifier": "Gathering",
      "CooldownHours": 0,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    },
    {
      "Identifier": "Sabotage",
      "CooldownHours": 0,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    }
  ]
}
```

- **`Identifier`**: Name that identifies the event. Do not change the ID values or the mod will break.
- **`CooldownHours`**: How many ingame hours have to be waited until event can start again
    - If set at "0" the mod will use the Game Default value for the cooldown for the event OR for mod events it will be the mod default cooldown.
- **`InfluenceRequirement`**: Amount of regional influence needed for the event to run
    - If the value is below 0.0 (e.g. -1.0) the mod will NOT apply any requirements OR it will use the game default influence requirements.
    - Range 0.0 - 1.0
- **`RandomTimeRangePercentage`**: Random range that is applied to the cooldown hours if cooldown hours is not 0.
    - If the value is above 0.0 for example 0.2, the mod calculates random cooldown with 20% range.
    - Range 0.0 - 1.0


The **RegionActivity** Cooldown controls how often one of these following *Regional Events* trigger inside any given region: StealDeadDrop, CartelCustomerDeal, RobDealer, SprayGraffiti. These 4 events do not have a cooldown by default, but you can add it in the config if you want.

---
Example template value for the **Ambush** that is the same random cooldown and influence requirement as the game has by default, but overrides the default game cooldown calculation:
```json
    {
      "Identifier": "Ambush",
      "CooldownHours": 15,
      "InfluenceRequirement": 0.1,
      "RandomTimeRangePercentage": 0.625
    },
```
Result: The cooldown hours will be random between 6 - 24 hours and happens only in regions with more than 100 influence

---
Example template value for the **RegionActivity** that is the same random cooldown as the game has by default, but overrides the default game cooldown calculation:
```json
    {
      "Identifier": "RegionActivity",
      "CooldownHours": 30,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.625
    },
```
Result: The cooldown hours will be random between 11 - 49 hours 

---
Example: I want to have the Dealer robbing at higher influence regions only and increase cooldown for only this event
```json
    {
      "Identifier": "RobDealer",
      "CooldownHours": 50,
      "InfluenceRequirement": 0.6,
      "RandomTimeRangePercentage": 0.3
    },
```
Result: The cooldown hours will be random between 35 - 65 hours and it only happens in regions with higher than 600 influence

---
Example: I want to have the Sabotage feature less often
```json
    {
      "Identifier": "Sabotage",
      "CooldownHours": 120,
      "InfluenceRequirement": -1.0,
      "RandomTimeRangePercentage": 0.0
    }
```
Result: The cooldown hours for sabotage event will be exactly 120 ingame hours (5 ingame days in total)

---

### Modifying Cartel Dealers
You can customize the Cartel Dealers' settings through the **dealer.json** file.

1. Open the **XO_WithSauce-CartelEnforcer/Dealers** folder inside your **UserData** directory.
2. Open the **dealer.json** file. Its contents by default are:

```json
{
  "CartelDealerWalkSpeed": 2.8,
  "CartelDealerHP": 200.0,
  "CartelDealerLethality": 0.5,
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

- **`CartelDealerWalkSpeed`**: Adjusts the **walking speed** of Cartel Dealers (Range 1.0 - 7.0)
- **`CartelDealerHP`**: Sets the **total health points** for a Cartel dealer (Range 10.0 - 2000.0)
- **`CartelDealerLethality`**: Controls how lethal Cartel Dealers' weapons are (Range 0.0 - 1.0)
- **`CartelDealerWeapon`**: Specifies the **weapon** used by Cartel dealers. Supported values are: "M1911", "Revolver", "Knife" and "Shotgun".
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

1. Open the `XO_WithSauce-CartelEnforcer/Ambush/ambush.json` file.
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

1. Open `XO_WithSauce-CartelEnforcer/Ambush/default.json`.
2. You can only **modify** the values here; do not add or remove any ambushes.
3. If the game receives a new update, delete `default.json` to ensure your configuration is up to date. It will be recreated the next time you load a save.

#### Modifying Ambush Settings

1. Open `XO_WithSauce-CartelEnforcer/Ambush/settings.json`.
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
    "AfterDealAmbushEnabled": true,
    "AmbushTriggerProbability": 0.8,
    "AmbushWeaponLethality": 0.33
}
```

- **RangedWeaponAssetPaths** & **MeleeWeaponAssetPaths**: Paths to the weapon assets that get used in Ambushes.
    - Note: Must be a valid path and string is case sensitive (example: Machete melee weapon can't currently be loaded with "Avatar/Equippables/Machete")
- **MinRankForRanged**: Player Rank requirement that indicates when goons start using ranged weapons in ambushes.
- **AfterDealAmbushesEnabled**: When true by default, after player completes a deal an ambush can happen instantly after. When disabled these stop happening and ambushes are only triggered by positional triggers.
- **AmbushTriggerProbability**: Controls how much of the regional influence is taked into the calculation of chance.
    - Example: If you raise this value from 0.8 to 1.0 it increases the chance of an ambush occuring. Lowering it will decrease the chance.
- **AmbushWeaponLethality**: Controls the lethality of the Ranged and Melee weapons.
    - At 0.0 weapon lethality is at game default
    - At 1.0 weapon lethality is doubled

---

#### Modifying Drive By Events

The Drive By events triggers and trigger radius' in addition to the driving routes can be configured from the **XO_WithSauce-CartelEnforcer/DriveBy/driveby.json**

You can add new triggers, edit existing ones or remove them. When adding new triggers, make sure that the **spawnEulerAngles** are Euler and not default Quaternion and that the car is facing correct rotation at the start position. Both the start and end positions must be on the road.

The file contains all of the drive by triggers data with each entry containing the name, trigger position & radius, car spawning rotation and the driving start and end position.

Example of the .json file content:

```json
{
  "triggers": [
    {
      "name": "Suburbia Bus Stop",
      "triggerPosition": {
        "x": 110.39,
        "y": 5.36,
        "z": -111.69
      },
      "radius": 2.0,
      "spawnEulerAngles": {
        "x": 0.0,
        "y": 270.0,
        "z": 0.0
      },
      "startPosition": {
        "x": 144.68,
        "y": 5.6,
        "z": -103.69
      },
      "endPosition": {
        "x": 17.4,
        "y": 1.37,
        "z": -103.53
      }
    },
    ...
    {
      "name": "Suburbia Jeremys house",
      "triggerPosition": {
        "x": 69.55,
        "y": 5.93,
        "z": -117.93
      },
      "radius": 2.0,
      "spawnEulerAngles": {
        "x": 0.0,
        "y": 270.0,
        "z": 0.0
      },
      "startPosition": {
        "x": 144.68,
        "y": 5.6,
        "z": -103.69
      },
      "endPosition": {
        "x": 17.4,
        "y": 1.37,
        "z": -103.53
      }
    }
  ]
}
```

---

#### Modifying Cartel Stolen Items

1. Open `XO_WithSauce-CartelEnforcer/CartelItems/(slot number)_(organisation name).json`.
2. You can modify the values here and change quantity of items as you wish or add new ones. Make sure the item ID is always a valid id. You can also change cartel stolen balance.
3. If you want to reset the stolen items in the specific save, you can delete the file and it will get regenerated.

---

### Modifying Influence Changing Events

1. Open `XO_WithSauce-CartelEnforcer/Influence/influence.json`.
2. Each type of event and its corresponding influence change is listed in the file. 
3. Positive values means that Cartel Influence Increases and Negative values mean that Cartel Influence Decreases.
4. Values are divided by a thousand, meaning that for example 0.050 corresponds to 50 increase in Cartel Influence.
5. The file content is by default:

```json
{
    "interceptFail": 0.025,
    "interceptSuccess": -0.050,
    "deadDropFail": 0.025,
    "deadDropSuccess": -0.050,
    "gatheringFail": 0.025,
    "gatheringSuccess": -0.080,
    "robberyPlayerEscape": 0.025,
    "robberyGoonEscapeSuccess": 0.025,
    "robberyGoonDead": -0.080,
    "robberyGoonEscapeDead": -0.050,
    "sabotageBombDefused": -0.150,
    "sabotageGoonKilled": -0.050,
    "sabotageBombExploded": 0.200,
    "cartelDealerPersuaded": -0.100,
    "trucedGreetingChallenge": -0.150,
    "passiveInfluenceGainPerDay": 0.025,
    "cartelDealerDied": -0.100,
    "ambushDefeated": -0.100,
    "graffitiInfluenceReduction": -0.050,
    "customerUnlockInfluenceChange": -0.075
}
```

Note: Due to the way which the mod handles some of the influence changes, sometimes the displayed influence change is __not__ displaying the correct change and can rarely display the change twice for the event

---

### Modifying Allied Settings

1. Open `XO_WithSauce-CartelEnforcer/Allied/config.json`.
2. Each Cartel Dealers "Cut" and "Signing Fee" is defined in this file
3. Additionally the Persuade Cooldown (in-game) minutes and Supply Quest Cooldown (in-game) Hours are defined in this file


4. The file content is by default:

```json
{
  "WestvilleCartelDealerCut": 0.3,
  "WestvilleCartelSigningFee": 6000.0,
  "DowntownCartelDealerCut": 0.4,
  "DowntownCartelSigningFee": 12000.0,
  "DocksCartelDealerCut": 0.5,
  "DocksCartelSigningFee": 18000.0,
  "SuburbiaCartelDealerCut": 0.55,
  "SuburbiaCartelSigningFee": 24000.0,
  "UptownCartelDealerCut": 0.6,
  "UptownCartelSigningFee": 36000.0,
  "PersuadeCooldownMins": 60,
  "SupplyQuestCooldownHours": 48
}
```

- **(Region)CartelDealerCut**: Amount of money the cartel dealer takes from each deal (percentage)
    - A number in range of 0.0 to 1.0 (0.0 = Dealer takes no cut, 1.0 = Dealer takes all of the money from deals)
- **(Region)CartelSigningFee**: Amount of money the cartel dealer needs to be paid to start working for the player
- **PersuadeCooldownMins**: Minutes in-game that the player must wait until they can attempt to persuade a Cartel Dealer again
    - Note: Must be an Integer without decimal point.
- **SupplyQuestCooldownHours**: Hours in-game that the player must wait until the Supply Quest can trigger again
    - Note: Must be an Integer without decimal point.


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
    - `Left CTRL + U`: Generate the Infiltrate Manor Quest dialogue option for Ray, without checking prerequirements.
    - `Left CTRL + P`: Instantly spawn a Cartel Gathering at a random location
    - `Left CTRL + N`: Start a Sabotage Event at nearest supported business
    - `Left CTRL + O`: Steal back the nearest customer to the player without checking prerequirements
    - `Left CTRL + I`: Start the Allied Intro Quest (note: might cause errors or break the game if not truced)
    - `Left CTRL + K`: Start the Allied Supplies Quest (note: might cause errors or break the game if not truced)

---

### In Multiplayer

For multiplayer to function correctly, all players must have the same configuration content inside the `UserData/XO_WithSauce-CartelEnforcer` folder

Most of the events and activities added by this mod **do not** support multiplayer yet.

---

###

**Note:** The configuration files and directory structure described in this document will be created automatically in the `UserData` directory if the files are missing.

---

Contribute, Build from Source or Verify Integrity -> [GitHub](https://github.com/XOWithSauce/schedule-cartelenforcer/)
