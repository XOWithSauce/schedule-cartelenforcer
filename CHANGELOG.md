# Version 1.7.0
- Compiled against 0.4.1f12 latest default / alternate game versions for the patch notes released today
- Added new Event "Business Sabotage" where Cartel attempts to disrupt your money laundering operations by blowing up businesses
- Added configuration value "businessSabotage" into the config.json file that allows disabling the newly added event
- Added extra safeguard logic to the fleeing phase of a robbery event for the goon
- Changed code for listeners and actions call back events to match latest source code and to track them during their usage
- Removed cartel related bug fixes implemented by the mod now that the patch fixes them in default game source
- Fixed 1 Drive-by event location having incorrect end position
- Fixed a bug where in the Car Meetup quest goons could infinitely trigger the combat and weapon wielding script due to bug with listeners
- Fixed a bug in the Defeat Enforcer quest where the sprinting speed up during boss combat rage stage would not work
- Fixed a bug in the Infiltrate Manor end game quest where the property lights in mansion would not be enabled during the quest
- Fixed a bug where during Defeat Enforcer quest the quest title can display higher numbers than intended for the dead drop stealed count and ambushes defeated count
- Removed redundant callback code from goons during the Infiltrate Manor quest combat stage
- Removed bug fix for drive by implemented in 1.6.1 for static vehicle bug


# Version 1.6.1
- Compiled against 0.4.1f5 latest default / alternate game version for Halloween + Sewers Update
- Added Ambush triggers into the sewers in the ambush.json file (note: these do need to have y value defined in spawn pos to be exact negative coordinate, unlike what the documentation currently states)
- Added configuration support for changing Graffiti rewarded Cartel Influence Reduction (graffitiInfluenceReduction in influence.json)
- Added configuration support for Ambush settings, changing the Weapon Arrays, Ranged weapon usage rank and also flag that allows disabling the after deal ambushes where ambush spawns randomly after deals.
- Added configuration value in config.json to allow for changing Drive-By frequency (range -1.0 -> 1.0)
- Changed the way which Drive-by events frequency and cooldowns are handled to fix a bug where the cooldown reduction is essentially doubled
- Increased robbery feature adrenaline boost duration for the robber goon so they run longer after looting dealer body
- Fixed a bug again with mod added quests where saving the game would cause crashes occasionally in IL2CPP version, as a tradeoff now the quest state and quest entry state can NOT be changed with ingame console commands
- Fixed 1 gathering location at Docks and added 2 new gathering locations
- Fixed a bug in the spawn logic of defender goons who can spawn after cartel dealer dies, where spawn locations would not be selected properly
- Fixed again frequency related logic where using lower activityFrequency than 0 would cause atleast 1 hour pass ingame to not account for slowed tickrate unintentionally
- Fixed a bug in IL2CPP code where extending the available goon pool npcs could cause crash with improper "is" casting
- Fixed a bug in Drive-by vehicle where on spawn it would have land vehicle marked as static and could not proceed with navigation
- Fixed a bug in the robbery feature where the goon could stay spawned after timeout and still kill the dealer when not intended
- Clean up code all over the place where relevant and add safeguards to prevent errors where applicable
- Added MelonLoader versioning related restrictions into assembly, now can only load on ML versions above 0.7.0 and in future this will have 0.7.1 excluded in IL2CPP version of the mod once ML 0.7.2 is out of nightly

# Version 1.6.0
- Added new Car Meetup Quest where player has to stop Cartel from transporting cocaine in the Northern Waterfront area
- Updated mod compatibility to match latest alternate-beta and beta versions for game version 0.4.0f7
- Changed the way which cartel dealers get assigned the default weapon from dealer config
- Removed code that allowed Cartel Dealers completed deals to change the respective customers affinity and addiction status
- Added new Drive-By Trigger in the Uptown region
- Added 1 new reward to the Unexpected Alliances quest to lower law intensity by 5% upon completion
- Removed Jeremy from Mini-Quest elgible NPCs since it now provides mod added stuff for car meetup
- Changed Unexpected Alliances quest to require defeating atleast 1 gathering in addition to stealing 2 cartel dead drops
- Changed the Manor quest enemies to have slighlty less total hp
- Lowered the Cartel Brute hp slightly also the rage stage hp regen slightly
- Fixed all the end game quest entry titles to have a bullet point "•" to match the game default quest syntax
- Fixed a problem in IL2CPP version of the mod where end game quest complete, fail and end methods would randomly throw NullReferenceExceptions or ViolationAccessExceptions probably due to networking logic or garbage collection
- Fixed 2 gathering location in the Uptown and northtown regions being at the incorrect position
- Fixed a bug where gathering event enemies would keep their drinking or smoking animations active during combat
- Fixed a bug where gathering event enemies would not stop the rotate or talk animations when combat has started


# Version 1.5.8
- Hotfixed 1 error in IL2CPP where loading a resource for the Cartel Dealer Weapon would have an incorrect "as" casting
- Forgot to include the clamping mechanism in Config Loader for the new influence.json configuration values
- config.json file in releases had debug mode set to true, which should be false by default. Re-released with the value set to false.

# Version 1.5.7
- Added new Influence Config which allows changing almost all events that reduce or add cartel influence
- Added new feature where killing cartel dealers has a chance to spawn 2 cartel goons nearby to attack player
- Added new configuration value to config.json "defaultRobberyEnabled" now allows disabling the game default robberies where player would only get a message saying dealer has been robbed
- Added new Ambush Positions to ambush.json
- Added new Drive By positions and increased some of the existing ones radius'
- Added new worldspace text into the Mini Quest steal cartel deaddrop, telling the player to hurry up.
- Modified the influence rewards from events to be slightly lower overall to compensate for the new ways of reducing or adding cartel influence
- Adjusted the default values in dealer.json to match better with current state of the mod and to optimize the dealer behaviour
- Changed the way guns get assigned to goons in events to make them not drop it on forceful impact
- Changed all mod handled contracts for Cartel Dealers to zero out any XP in the contract instead of having 1 xp per contract
- Changed the way which game default CartelCustomerDeal events behave for locked customers to make it more reliable and faster to deactivate when there are no elgible customers for the game default event
- Changed the game default CartelCustomerDeal events to potentially fill Cartel Dealer inventory with higher quality product, if their locked customer needs it.
- Changed Gathering location description of "northern wharf" to be "northern waterfront" because thats what the place actually is 
- Changed the Real Robbery goon to have 160 hp instead of 100 to make it more probable that it will win the event solo against dealer
- Increased the radius which replacement Cartel Dealers get searched for nearby the customer for the Intercept Event when the respective regions Cartel Dealer is occupied. Up from 60 to 80 units distance.
- Increased the radius which RealRobbery occurs at up from 60 to 120 units distance between player and dealer
- Fixed a bug where during Real Robbery while the Goon is looting dealer and the goon dies, it would keep moving items into inventory and running the event for a while
- Fixed a bug in goons where after they despawn, they can still be in active combat and attack player while invisible
- Fixed a bug where the cartel dealer would be able to free roam when enhanced dealer was off

# Version 1.5.6
- Simplified the way which Cartel Dealers get assigned with players pending deals to reduce lag.
- Fixed a bug where the Cartel Dealers would get assigned contracts even after bedtime when they are supposed to head back inside
- Fixed a bug where the Cartel Dealers would not update their activity status while cartel is other than hostile
- Fixed a bug where the Cartel Dealers would not trigger Safety if activity is low enough and safety is enabled, when cartel is other than hostile
- Fixed a bug where after stay inside ends or the deal signal ends, the cartel dealer could inproperly fail contracts when not intended
- Fixed a bug where the code would try to make dead or knocked out cartel dealers walk to locations in their free time
- Fixed a bug where the code would try to make cartel dealers walk to locations outside of their dynamic time range
- Fixed a potential bug in loading the cartel stolen items
- Fixed a bug in the Cartel Dealer config where DealerActivityIncreasePerDay value was incorrectly set to the DealerActivityDecreasePerKill value
- Fixed a bug in the Cartel Dealer config where the StealDealerContractChance value was incorrectly set to the StealPlayerContractChance value
- Added some coroutine safety checks to prevent mod from throwing errors in various scenarios when exiting to menu or reloading save

# Version 1.5.5
- Changed the way which intercept deals event evaluates active player contracts. The new method skips parsing ui elements totally which was a dumb but working method previously. Should have less lag now.
- Added new calculation logic for intercept deals event where it now should dynamically evaluate both the random contract and cartel dealer who intercepts. This change should make more contracts succesfully accepted for the intercept deals event and targets potentially nearby cartel dealers for that event.
- Changed the Intercept Deals event to now correctly pertain their contract even after cartel dealer is killed. This change also allowed the original XP to be assigned back to the existing contract. Additionally the event now awards correct amount of XP when completed by player and only 1 xp when completed by cartel dealer. Previously it was 50% of original XP regardless of who completes.
- Lowered the NPC Relationship Status change in Intercept Deals Event down from 0.25 to 0.10 based on who completed contract. (0.00 - 5.00 range)
- Changed Cartel Dealers Walking behaviour to only target locations, within their respective regions. This should fix a bug where intercept dealer would walk from across the map and constantly fail intercept deals due to walking for 2 ingame hours.
- Lowered the default chance values for Cartel Dealers taking Players hired dealers active deals and also players pending offers, down from 20% and 20% to 3% and 3%. This was overlooked because the evaluation runs every ingame hour and each cartel dealer has sequential chance to first take 20% chance for dealer contract, then another 20% if the previous failed, and because this gets calculated for 5 dealers, the overall chance for any given contract to be taken is extremely high every ingame hour. Higher values prevent Intercept Deals from happening due to Cartel Dealers being reserved for these.
- Adjusted the Gatherings Frequency to be less common overall by roughly 1 ingame hour across any given dealer activity status (how many killed)
- Increased the Cartel Influence change from defeating a gathering up from 50 to 100
- Fixed a bug in the Cartel Gatherings when player has negative Cartel Dealer activity status, the Gathering goons would not attack player properly
- Fixed a bug in the function controlling Dealer Activity timings based on Cartel Dealer killings. Bug caused killing dealers to not decrease their activity as it should have.
- Set the Cartel Dealer Aggression to be at 1f instead of whatever it is at default. Cartel Dealers occasionally ran away when shot at, and because of movement speed multiplier they are running faster than what was intended this should make them attack back more / always.
- Fixed some bugs related to default cartel dealer behaviours and intercept deal event being conflicting. This should allow consistently the dealer to have intercept event deals even beyond 04:00 default maximum stay inside time limit.
- Fixed a bug in Manor end game quest where Ray would not properly reset their schedule after quest was failed
- For some reason Versioning number has been forgotten to change since 1.5.1 inside the Melon Loader entry, now that is up to date and should show 1.5.5 every time mod loads

# Version 1.5.4
- Changed the cartel dealers behaviour so that they can roam the map while cartel is in status unknown, friendly or defeated in addition to being hostile, this wont trigger the dealers stealing your or your dealers contracts before cartel turns hostile
- Changed the code default generated values for the dealer.json config to be the same as in documentation since they were different 
- Changed Cartel Gatherings to be a regional task in code structure and unlocking new gathering locations based on unlocked regions. This aligns better with quest and playthrough.
- Cartel gathering locations are now picked at random from all unlocked gathering locations instead of all possible locations.
- Added cartel gathering locations to all regions alongside descriptions
- Cartel Gathering locations can now be revealed by bribing Mini Quest giving NPCs
- Made Cartel Gatherings more frequent and now they should appear also during early game even before cartel is "hostile"
- Cartel Gathering goons have now in their inventory Stolen Money and Items
- Cartel gathering goons now do the smoke and drink animations on random basis instead of having always enabled
- Changed the Cartel Gathering goons to do random voice lines and rotate occasionally
- Increased Cartel gathering max time to 3 ingame hours
- Defeating cartel gathering decreases the region influence by 50 (up from 25)
- Changed result if gathering is not defeated, the regional influence will rise by 25 (up from 5), but only up to 400 regional influence.
- Cartel Gathering has always 1 loot goblin goon, who has Cartel Stolen Items in their inventory, if cartel has stolen items from your dealers
- Cartel Gathering goons always have their inventories filled with $500 of stolen money if cartel has stolen money from your dealers
- Changed the Cartel Dealer processing in Handovers, to override the completed deal payment to 0, this fixes the bug where in early game the dealers will do lots of contracts and display their rewards to player
- Added more Mini Quest giving NPCs: Dan, Jeremy, Marco, Herbert
- Changed mini quest generation to only work during 07:00 to 24:00
- Lowered the chance of Mini Quest being givven to player by any given npc, but alternatively if the random roll is not met the NPC will give out Gathering location if one is active
- Added worldspace text templates where npc tells the location to player
- Added support for saving the stolen quantity of cash in each save in the cartel stolen items json file
- Removed redundant keybind inputs for safe placement and goon array extend from Debug mode
- Removed redundant code for deal signal setting since it gets replaced automatically anyways
- Fixed a bug where dealer would stay stuck outside and toggling active contract forever
- Fixed a bug where cartel dealer hp would not set based on config
- Changed the Cartel Dealer copied contracts to not increment completed contracts

# Version 1.5.3
- Hotfix a mistake in previous version where the Cartel Dealer would still send messages to player

# Version 1.5.2
- Changed the Cartel Dealer logic to be its own system and is now driven by config values, toggleable by enhancedDealers = true, a separate system now from intercept deals, both should work intertwined with same cartel dealer objects
- Added new event Cartel Gatherings where Cartel Goons chill outside during daytime.
- Added logic to extend the spawnable cartel goon amount from 5 -> 10 to support Cartel Gatherings reserving always atleast 3
- Added to Infiltrate Manor quest logic to spawn a small safe with loot inside of it in upstairs rooms
- Added new value for changing the End Game Quests Monologue speed to slow it down
- Added a new drop to the Unexpected Alliances boss, it can now drop a shotgun with 33% chance, additionally now always drops shotgun shells
- Added new feature to the Unexpected Alliances boss, a Rage Stage where the boss starts drinking Cuke to regain health, also additionally can start sprinting faster towards player
- Added new Timeout caps to Infiltrate Manor quest where it has to be completed by 03:59 or it fails
- Changed Infiltrate Manor end game quest to fail if Police spots player inside manor
- Changed End Game Quests to scale in difficulty, based on all regions influence in total. Higher overall influence will result in higher difficulty.
- Changed End Game Quests XP Reward to scale with the same difficulty scalar.
- Changed the Infiltrate Manor Quest to only open the Manor door after the Break In quest state has been activated
- Changed the Loot Pick order for Infiltrate Manor Quest rewards so that the Safe can be filled with more rare items first
- Changed both end game quests minimum requirement to be 1 customer from Suburbia region instead of 5
- Increased the amount of Mini Quests generated each day, from an average of 1 dialogue per day to roughly 4 dialogues per day
- Increased the chance of generating Mini Quests for locked customers from 30% to 80%
- Removed the 30% chance to skip messaging for Manny in Unexpected Alliances quest
- Fixed a bug where Mini Quest dialogue option would display NaN
- Fixed a bug where Real Robbery feature robber would be invincible while escaping
- Fixed a bug where after Infiltrate Manor quest the door was not reset closed
- Fixed a bug where the Manor Goons would stop combat early
- Fixed a bug where the Hourpass functions of end game quests would not get removed properly
- Added safeguards to the end game quests min pass methods which dont seem to get deleted from MinPass functions in Time Manager
- Fixed a bug where in Unexpected Alliances end game quest the Contact NPC will go inside a building and turn invisible
- Fixed a bug where in Unexpected Alliances end game quest Despawning the Contact NPC would cause errors
- Fixed a bug where in end game quests longer battles or not seeing player for long enough would cause enemies to lose aggro 
- Fixed a bug where end game quest would not be removed from the active quests causing errors when game is saved after completion
- Fixed a bug in Infiltrate Manor quest where Ray would not reset back to their normal schedule of smoking next to courthouse from 18:15-19:00, but 18:15-22:00
- Fixed a bug in end game quests where enemies would drop their weapons when hit with forceful impact
- Fixed a bug where Cartel Dealers would message player when not having product
- Fixed a bug where Cartel Dealers would cause Player Relations with Customers to rise before being unlocked
- Fixed a bug in influence system where it would try to reduce or add influence from northtown
- Fixed a bug where Cartel Goons would sometimes when spawning to world, have their gravity enabled while despawned under map, causing them to fall infinitely under the map while despawned.
- 

# Version 1.5.1
- Added new Manor End game quest
- Fixed cartel dealers standing still in their apartment doors, now they will try to travel to random locations and should consistently deal
- Changed cartel dealers start time to be randomized between 16:20 and 16:59
- Fixed an issue where cartel dealer would not automatically take contracts pending for player 
- Fixed an isse where cartel dealer would not automatically take contracts active for player dealers
- Fixed a critical issue where players unlocked customers could get removed from the static Customer instance unlocked customers
- Fixed a logic issue with end game quests where the dialogue index was not saved so there was a possibility to accidentally remove incorrect dialogue option
- Fixed a critical issue where rarely in IL2CPP the End Game Quest entries would not get correctly mapped or the entry original objects get garbage collected, then the pointer to that quest entry object during the quest is a random pointer to the games memory and this caused multiple types of errors where Quest proceeding will cause the game to start casting random memory locations to IEnumerable
- Reverted the change in v1.5.0 where dealers get robbed inside building, back to dealers not getting robbed inside buildings due to community feedback

# Version 1.5.0
- Added a new end game mission that can be acquired from Manny (a.k.a. Fixer in Warehouse)
- Added new Configuration values cartelDealChance and endGameQuest
- Added new functionality to the mod where Cartel Dealers will now go out of their homes to deal more, based on the cartel deal chance and activity frequency. This is additional to the normal customer dealings they have. This functionality will be ran alongside the interceptDeals configuration value since its behaviour is similiar.
- Added new functionality to the mod to override Cartel Dealer behaviour and daily events timeline. They now leave their homes at earliest 16:20 and will stop dealing at 4:20.
- Intercept event base frequency raised to 120 up from 90, now intercepts will happen a bit less at default activity frequency. Intercept event Lowest frequency was lowered to 240 from 480. Now at activity frequency -1.0 intercepts will happen more.
- Changed Mini Quests feature to be better chance at 12:00 - 18:00 to give quests by the NPC (business hours rumours)
- Changed the logic of Players hired Dealers getting robbed, back to allowing Dealers getting robbed if they are inside building but outside the range where the real robberies event can run, this will inadvertedly increase the amount of player hired dealers getting robbed.
- Fixed the Mini Quests dialogue incorrectly displaying 2 prepositions in the phrase
- Tried to fix intercept event throwing errors if player invalidates contract by completing it early into the feature

# Versions 1.4.1 and 1.4.2 for IL2CPP
- Fixed bug that caused robbery to always throw errors when doing message robbery
- Fixed bug that caused saving cartel stolen items to throw errors

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