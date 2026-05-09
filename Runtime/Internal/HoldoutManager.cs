using UnityEngine;

namespace PromptSurgeSDK.Internal {
    internal static class HoldoutManager {
        private const string Key = "ps_holdout";

        // Assigned once per device and cached. 10% probability.
        internal static bool IsHoldout {
            get {
                if (PlayerPrefs.HasKey(Key))
                    return PlayerPrefs.GetInt(Key) == 1;

                var assigned = Random.value < 0.10f;
                PlayerPrefs.SetInt(Key, assigned ? 1 : 0);
                PlayerPrefs.Save();
                return assigned;
            }
        }
    }
}
