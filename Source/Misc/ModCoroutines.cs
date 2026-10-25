using MelonLoader;
using System.Collections;

using static CartelEnforcer.DebugModule;

namespace CartelEnforcer
{
    /*
    Serves as a wrapper for MelonCoroutines for when the routine is not yielded instantly. 
    Required because functions must yield break whenever save is unloaded.
    */
    public class ModCoroutines
    {
        public static List<object> _activeCoroutines = new List<object>();
        public static bool isStopping = false;
        public static void Start(IEnumerator routine)
        {
            _activeCoroutines.Add(MelonCoroutines.Start(routine));
            return;
        }
        public static void StopAll()
        {
            if (_activeCoroutines.Count == 0) return;
            Log("Stopping coroutines count: " + _activeCoroutines.Count);
            isStopping = true;
            for (int i = _activeCoroutines.Count - 1; i >= 0; i--)
            {
                if (_activeCoroutines[i] != null)
                    MelonCoroutines.Stop(_activeCoroutines[i]);
            }
            _activeCoroutines.Clear();
            isStopping = false;
        }
    }
}