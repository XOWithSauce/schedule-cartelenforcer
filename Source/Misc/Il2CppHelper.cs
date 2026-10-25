
namespace CartelEnforcer
{
    /*
     TODO: rest of the use cases theres plenty in code that can be consolidated here
        - in #else il2cpp usings block import the static class from the il2cpp namespace
     */
#if IL2CPP
    namespace Il2CppCollections
    {
        public static class Il2CppCollectionsHelper
        {
            // Any()
            public static bool Any<T>(this Il2CppSystem.Collections.Generic.List<T> list)
            {
                return list != null && list.Count > 0;
            }

            // Any(x => x.Id == "")
            public static bool Any<T>(this Il2CppSystem.Collections.Generic.List<T> list, Func<T, bool> predicate)
            {
                if (list == null || predicate == null) return false;

                for (int i = 0; i < list.Count; i++)
                {
                    if (predicate(list[i]))
                        return true;
                }
                return false;
            }
        }
    }
#endif
}