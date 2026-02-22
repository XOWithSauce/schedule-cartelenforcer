
using MelonLoader;
using System.Reflection;
using static CartelEnforcer.CartelEnforcer;
using static CartelEnforcer.DebugModule;

namespace CartelEnforcer
{
    public class ModPrefsHandler
    {
        public MelonPreferences_Category modConfigCategory;

        // Basically just create prefs for Mod basic config and on updated rewrite the config json
        public void SetupMelonPreferences()
        {
            modConfigCategory = MelonPreferences.CreateCategory($"{BuildInfo.Name} {BuildInfo.Author}", BuildInfo.Name);

            modConfigCategory.CreateEntry(
                "debugMode", currentConfig.debugMode,
                display_name: "Debug Mode Enabled",
                description: "Enable keybinds & display visual cues"
            );

            modConfigCategory.CreateEntry(
                "driveByEnabled", currentConfig.driveByEnabled,
                display_name: "Drive By Enabled",
                description: "Enables drive-by events where Thomas shoots the player"
            );

            modConfigCategory.CreateEntry(
                "realRobberyEnabled", currentConfig.realRobberyEnabled,
                display_name: "Real Robbery Enabled",
                description: "Enables robberies where a goon is spawned to fight the dealer"
            );

            modConfigCategory.CreateEntry(
                "defaultRobberyEnabled", currentConfig.defaultRobberyEnabled,
                display_name: "Default Robbery Enabled",
                description: "Enables the robberies where you only get a text message"
            );

            modConfigCategory.CreateEntry(
                "miniQuestsEnabled", currentConfig.miniQuestsEnabled,
                display_name: "Mini Quests Enabled",
                description: "Enables mini quest generation for finding cartels dead drops"
            );

            modConfigCategory.CreateEntry(
                "interceptDeals", currentConfig.interceptDeals,
                display_name: "Intercept Deals Enabled",
                description: "Enables an event where Cartel Dealers try to steal your active deals"
            );

            modConfigCategory.CreateEntry(
                "enhancedDealers", currentConfig.enhancedDealers,
                display_name: "Enhanced Dealers Enabled",
                description: "Use custom behaviour for Cartel Dealers"
            );

            modConfigCategory.CreateEntry(
                "cartelGatherings", currentConfig.cartelGatherings,
                display_name: "Cartel Gatherings Enabled",
                description: "Enables an event where Cartel Goons gather around randomly during the day"
            );

            modConfigCategory.CreateEntry(
                "businessSabotage", currentConfig.businessSabotage,
                display_name: "Business Sabotage Enabled",
                description: "Enables an event where a Cartel Goon tries to plant a bomb at your business"
            );

            modConfigCategory.CreateEntry(
                "stealBackCustomers", currentConfig.stealBackCustomers,
                display_name: "Steal Back Customers Enabled",
                description: "Enables a feature where the Cartel can steal back your customers and lock them"
            );

            modConfigCategory.CreateEntry(
                "alliedExtensions", currentConfig.alliedExtensions,
                display_name: "Allied Extensions Enabled",
                description: "Allows you to hire cartel dealers and partake in supply quests while enabled. Requires End Game Quests to be enabled."
            );

            modConfigCategory.CreateEntry(
                "endGameQuest", currentConfig.endGameQuest,
                display_name: "End Game Quests Enabled",
                description: "Enables the generation of custom quests for Cartel"
            );

            for (int i = 0; i < modConfigCategory.Entries.Count; i++)
            {
                string id = modConfigCategory.Entries[i].Identifier;
                void ThisEntryChanged(object objOld, object objNew)
                {
                    OnEntryChange(id, objOld, objNew);
                }
                modConfigCategory.Entries[i].OnEntryValueChangedUntyped.Subscribe(ThisEntryChanged);
            }

            //Log("Melon preferences created");
        }

        public static void OnEntryChange(string identifier, object objOld, object objNew)
        {
            // instead of sync config this can just auto apply on changed
            FieldInfo[] modConfigFields = currentConfig.GetType().GetFields();
            foreach (FieldInfo field in modConfigFields)
            {
                if (!field.Name.Contains(identifier)) continue;
                // Prevent disabling end game quest if allied extension is enabled
                if (field.Name.Contains("endGameQuest") && currentConfig.alliedExtensions && !(bool)objNew)
                {
                    break;
                }

                field.SetValue(currentConfig, (bool)objNew);
            }

            // Instantly reflect the melon pref change in .json
            ConfigLoader.Save(currentConfig);
        }
    }
}