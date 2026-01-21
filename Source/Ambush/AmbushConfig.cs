
using UnityEngine;

namespace CartelEnforcer
{
    // Serializer for base CartelAmbushLocation
    [Serializable]
    public class NewAmbushConfig
    {
        public int mapRegion = 0; // Maps out to 0 = Northtown, 5 = Uptown
        public Vector3 ambushPosition = Vector3.zero; // Needed for detection radius check, instantiate new monobeh base at this location
        public List<Vector3> spawnPoints = new(); // note min 4 spawn points, instantiate as child obj new empty transform objects to fill base class AmbushPoints variable
        public float detectionRadius = 10f; // How far player can be at maximum from ambushPosition variable, default 10
    }

    // Serialize this class to json file for configure
    [Serializable]
    public class ListNewAmbush
    {
        public List<NewAmbushConfig> addedAmbushes = new List<NewAmbushConfig>();
    }

    // from Ambush/settings.json, defaults to just 2 weps m1911 knife, paths case sensitive for resource.load?
    [Serializable]
    public class AmbushGeneralSettingsSerialized
    {
        public List<string> RangedWeaponAssetPaths;

        public List<string> MeleeWeaponAssetPaths;

        public int MinRankForRanged = 2; // default 3 see below
        public bool AfterDealAmbushEnabled = true; // By default in source code its enabled (see Ambush.ContractReceiptRecorded), patched prefix true/false by this condition
    }

}
