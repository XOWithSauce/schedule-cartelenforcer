

using MelonLoader.Utils;

namespace CartelEnforcer
{
    /// <summary>
    /// Consolidates all the paths that mod might use in different installations
    /// </summary>
    public static class ModDataPaths
    {

        // Folder where all userdata is
        private readonly static string BASE_USERDATA_NAME = "XO_WithSauce-CartelEnforcer";

        // For when user uses thunderstore mod manager it creates the following name for the folder where it puts the folders, depends on backend version
        private readonly static string TS_PACKAGE_NAME = "XO_WithSauce-CartelEnforcer_";
#if MONO
        private readonly static string packagePathUserData = Path.Combine(MelonEnvironment.UserDataDirectory, TS_PACKAGE_NAME + "MONO", BASE_USERDATA_NAME);
#else
        private readonly static string packagePathUserData = Path.Combine(MelonEnvironment.UserDataDirectory, TS_PACKAGE_NAME + "IL2CPP", BASE_USERDATA_NAME);
#endif
        // For when user drags and drops the UserData folder from manual download, this is fallback checked path for the mod userdata folder
        private readonly static string manualPathUserData = Path.Combine(MelonEnvironment.UserDataDirectory, BASE_USERDATA_NAME);


        // Then for each config file or persistent data file it depends on the subfolder level

        public static readonly string pathModConfig = "config.json"; // Mod Folder in UserData root level

        public static readonly string pathAmbushes = Path.Combine("Ambush", "ambush.json");
        public static readonly string pathDefAmbushes = Path.Combine("Ambush", "default.json");
        public static readonly string pathSettingsAmbushes = Path.Combine("Ambush", "settings.json");

        public static readonly string pathDriveBys = Path.Combine("DriveBy", "driveby.json");
        public static readonly string pathDealerConfig = Path.Combine("Dealers", "dealer.json");
        public static readonly string pathCartelStolen = "CartelItems"; // Directory, Filenames dynamic {saveslot num}_{organization}.json
        public static readonly string pathInfluenceConfig = Path.Combine("Influence", "influence.json");

        public static readonly string pathAlliedConfig = Path.Combine("Allied", "config.json");
        public static readonly string pathAlliedPersist = Path.Combine("Allied", "QuestData"); // Directory, Filename dynamic {saveslot num}_{organization}.json

        public static readonly string pathEventFrequency = Path.Combine("EventFrequency", "config.json");
        public static readonly string pathEventFrequencyPersist = Path.Combine("EventFrequency", "Cooldowns"); // Directory, Filename dynamic {saveslot num}_{organization}.json

        private static bool hasCheckedInstallationPath = false;
        private static bool isModManagerInstallation = false;

        // one helper function to merge mod paths with installation
        public static string GetPathTo(string modDataDestination)
        {
            if (!hasCheckedInstallationPath)
            {
                if (Directory.Exists(packagePathUserData))
                {
                    //MelonLogger.Msg("Installation is a mod manager installation");
                    isModManagerInstallation = true;
                }

                if (Directory.Exists(manualPathUserData))
                {
                    //MelonLogger.Msg("Installation is a manual installation");
                    isModManagerInstallation = false;
                }

                hasCheckedInstallationPath = true;
            }
            string userDataPath = isModManagerInstallation ? packagePathUserData : manualPathUserData;
            return Path.Combine(userDataPath, modDataDestination);
        }


    }
}