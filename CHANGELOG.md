# Version 1.4.0
- Code base change now divided to files and conditional statements instead of having over 3k lines in 1 file for 2 different builds
- Performance boost from object state management and codebase proof reading and fixing
- Fixed a bug where the Dealer would start targetting itself in combat, resulting in them spinning around and trying to hit themselves. This happened only in robberies and they lost every fight.
- Changed the Drive By Shooting speed to be static at 4 bullets per second, but can be slower because of randomization
- Fixed a bug in the IL2Cpp version where the saved items serializer would still not work, again due to coding mistake missing serializer attribute on the class
- Changed Mini Quest duration to be static 60 seconds instead of random from 60-120
- Changed Intercept Deals feature grace time before Cartel Dealer starts walking, from random 10-30 seconds to static 30 seconds
- Removed Debug key to log the inventory contents as it was only used for testing
- Changed mini quest to only generate when cartel is hostile, previously it generated in other states too but the dialogue option was locked
- Changed the Release version of the mod to not contain debug logs anymore, meaning that Debug Mode will not Log anything anymore. This reduces filesize by over 50kb and should offer a performance boost. Logging can be enabled by building from source code in GitHub with Debug configuration enabled.
- Robbery feature now rolls random chance to give robber different weapons: 50% chance to be only fists, 45% chance to be a knife, 5% chance to be a pump shotgun
- Fixed Robbery feature message robberies targetting dead or inside building dealers, which was unrealistic behaviour
- Alot of other miscellanious undocumented changes that have happened but forgot to write down, these are mainly for performance boosts and other il2cpp related bugs.





# Version 1.3.2
- Changed the Mini Quest system to prefer Unlocked NPCs. If the NPC is Locked, there is only 30% chance to generate the miniquest. Otherwise the random selection and generation will be 100% chance if locked. The Dialogue still has its own randomisation allowing the NPC to decline giving quest.
- Changed the Drive By shooting speed to slower, from max 10 bullets / second and min 5 bullets / second -> down to max 4 bullets/second and min 2 bullets / second
    - Drive By event and Death was nearly unavoidable at some locations when triggered, and the shooting logic worked too well and sprayed 5 bullets a second consistently at nearby range... Dying over and over again to same drive by location is not intended behaviour.
- Fixed a bug in where the Stolen Items Persistent system would not find the correct item while trying to save it
- Fixed a bug in IL2Cpp version where the Stolen Items persistent system would cause errors while loading or saving items
- Fixed a bug in IL2Cpp version where the activity frequency system would fail to cast instances, not causing errors but indexing different activities incorrectly
- Fixed a bug where the stolen items would be updated while they are being saved causing errors
- Fixed dealer robbing to target only unlocked and recruited dealers (previously locked region dealers got robbed)
- Fixed dealer robbing targetting sometimes dead or knocked out or inside building dealers
- Fixed a coding mistake where the Robbery System time evaluation would not correctly count time, only lasting 12 seconds instead of 60
- Fixed a coding mistake in the persistent stolen items system where instead of evaluating Packaging ID, it was evaluating Item ID, causing the calculation for Stolen Quantity to be incorrect
- Fixed a bug where the Intercept Deals in the First Game region would cause errors, because there is no designated cartel dealer for the region
- Fixed a bug where after Drive By Event Thomas would keep floating in the air where the Car despawned at
- Tried to fix a bug where the cartel dealer starts bugging out at 4am
- Fixed other il2cpp bugs and errors
- OnUpdate method logic fixes

# Version 1.3.1 
- Fixed a bug where time stops randomly
- Fixed miscellanious bugs in il2cpp version
- Changed the intercept deals timer to tick faster
- Tried to fix a bug where the robbery success evaluation would not work if the robber runs fast enough
- Fixed the way at which Deal Intercepts get evaluated
- Fixed a bug where the intercept deal would not evaluate player having succesfully stopped the intercept
- Fixed the functionality of intercept deal to match readme descripption
- Added new checks at the end of intercept deal, if the intercept is stopped (contract ends) that increases relationship by 0.25 (0.0 - 5.0)

# Version 1.3.0
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




# Version 1.2.0
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

# Version 1.1.0
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