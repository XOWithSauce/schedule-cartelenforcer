# Version v1.3.1 
- Fixed a bug where time stops randomly
- Fixed miscellanious bugs in il2cpp version
- Changed the intercept deals timer to tick faster
- Tried to fix a bug where the robbery success evaluation would not work if the robber runs fast enough
- Fixed the way at which Deal Intercepts get evaluated
- Fixed a bug where the intercept deal would not evaluate player having succesfully stopped the intercept
- Fixed the functionality of intercept deal to match readme descripption
- Added new checks at the end of intercept deal, if the intercept is stopped (contract ends) that increases relationship by 0.25 (0.0 - 5.0)

# Version v1.3.0
- Added 4 new configuration values to allow individual frequency change of Ambush, Dead Drop Steal, Cartel Customer Deal and Cartel Robbery events frequencies
    - Default at 1.0 means that the event frequency is NOT capped at all and can happen every ingame hour (doesnt mean that it will happen every hour)
    - at 0.0 the event can happen only once every 2 ingame days at fastest
    - at -1.0 the event can happen only once every 4 ingame days at fastest
    - Note: These values dont change how often something happens, these values decide when something CAN happen next time (as opposed to activityFrequency which directly changes frequency of when something happens)
- Added new configuration value for Intercept Deals
    - New type of event where Cartel will Actively attempt to intercept Player deals
    - Based on activityFrequency deal CAN be intercepted at 1.0 every ingame hour and at -1.0 every 8 ingame hours and At 0.0 every 3 ingame hours.
    - Deals can Only be intercepted from 6pm to 4am only and Cartel must be Hostile
    - Only Deals with less than 5 hours left AND more than 1 hour and 30mins left can be intercepted by cartel
    - If player is closer than 40 units distance to the Customer whose deal is about to be intercepted, the intercept is cancelled.
    - More in Readme or Description
- Added Persistence for Cartel Stolen Items in stolen.json file
    - When Dealer gets robbed, their items are stored individually each save file.
- Added more debug controls, CTRL + L to log internal mod data, CTRL + I to log inventory content, CTRL + T to Intercept random deal
- Added new Harmony Patch to save the Stolen Items data whenever player saves the game
- Added function to the Exit Menu Prefix to save Stolen Items data when exiting to menu
- Completed the TryStartActivity and TryStartGlobalActivity Patches to match the funcitonality of original source code.
    - They are now equivalent to Transpliers, overwriting the entire function so that it is possible to control ambush, dead drops, etc. individual event frequencies
- Completely patched the TryRobDealer function to have original source code functionality alongside the existing features. This allows the code to store stolen items even if robbery was done via the "text message indication"
- Added more text message templates to send to player when robbery is about to start
- Added functionality for the Real Robbery to steal and store items
- Lowered the Influence Decrease in Real Robbery when the Robber dies now influence decreases by 25, previously 80
- Changed Robber Despawn timer to be 60 seconds up from 30
- Adjusted Robber Adrenaline boost to have faster movement speed and adjust more smoothly
- Changed Mini Quests to now ask for a dynamic payment price, based on the relations status with the NPC. If at best possible relations, the Mini Quest costs 100 cash. If at lowest possible relations it will cost 500 cash. Previously it was static 100 cash.
- Changed Mini Quest Refusal rate to be much lower and based on the relations with the NPC. At best possible relations there is only 40% chance to refuse to give the quest. At worst possible relations there is 70% chance to refuse.
- Changed Mini Quest to have the NPC tell player more often the exact location, based on the NPC Relation data, at best relation there is only 40% chance to tell the region and 60% chance to tell the exact location of the Cartel Dead Drop
- Changed Mini Quest Lower timerange to be 60 instead of 30 seconds to allow player to reach across the map
- Changed Mini Quest Rewards to Additionally give out some of the Cartel Stolen Items

- Fixed a bug where Dealer combat behaviour would not stop after defeating the robber
- Fixed a bug where the Robber would not travel to the correct building, but nearest door
- Fixed a bug where the Robbery function would not correctly evaluate when the Robber has succesfully escaped to the safehouse
- Fixed a bug where Robbery Goon Spawning would keep searching for a spawn position nearby indefinitely




# Version v1.2.0
- Added Configuration support for changing the Cartel Activity Frequency
    - Value *activityFrequency* in `config.json`
- Added Configuration support for changing the Minimum Cartel Influence requirements
    - Value *activityInfluenceMin* in `config.json`
- Added New Configuration values *driveByEnabled*, *realRobberyEnabled* and *miniQuestsEnabled*
- Changed the Default Debug Mode value to be false in code
- Added Functionality for changing all activity frequencies to be roughly 10 times faster at *activityFrequency* 1.0 and roughly 10 times slower at *activityFrequency* -1.0, Default Disabled at 0.0
- Added Functionality for changing all activity Influence requirements to be at 0 (Events will happen always) when *activityInfluenceMin* is at -1.0 and at 1000 (Events will only happen at max cartel influence) when at 1.0, Default Disabled at 0.0
- Added a New Mini Quest, which can be acquired from select NPCs (see readme for more info)
- Added a new event Drive By and 11 new area triggers (Orange Spheres in Debug Mode) (see readme for more info)
- Added methods to break coroutines when exiting to menu or loading last save to avoid errors
- Organized the startup sequence to be faster and reliable
- Added More Debug Mode Keybinds to trigger events manually:
    - Left CTRL + G to start Instant Drive By at nearest trigger
    - Left CTRL + H to give one of the select npcs a Mini Quest
- Changed the Real Robberies to evaluate current Region:
    - If Dealer Robbery is spawned then based on combat outcomes, the Regional Cartel influence will change

# Version v1.1.0
- Added a new feature to enhance the Dealer Robbing Mechanism in the base game
    - When player is within 60 units of a Dealer, that is about to get Robbed the feature gets triggered
    - Dealer will send a Text Message to the Player indicating they are getting robbed and asking for help
    - A Robber is spawned near the Dealer and they begin to fight
        - If the Robber dies or gets knocked out during fight the feature ends and nothing is stolen. 
        - If the Player runs away more than 90 units from the Robber, the Dealer will always succesfully defend the robbery.
        - If the fight lasts for more than 1 minute without resolution, the Dealer will always succesfully defend the robbery.
        - If the Dealer dies or gets knocked out, the next phase begins:
            - Robber gets a sudden Adrenaline boost, granting them Speed Boost and minor amount of Health Regeneration
            - Robber steals items from the Dealer inventory to their own inventory
            - Robber tries to reach nearest safehouse door, where a Cartel Dealer lives, ignoring combat during the escape
                - If Robber reaches the house they will despawn instantly
                - If Player kills or knocks out the escaping Robber they can steal back the items from the inventory.
            - If the Robber can not find any Cartel Dealer safehouse, then the Robber will try to Flee the Player for a maximum of 60 seconds, after which they will despawn