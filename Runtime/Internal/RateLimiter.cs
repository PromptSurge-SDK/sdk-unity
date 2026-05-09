using UnityEngine;

namespace PromptSurgeSDK.Internal {
    internal static class RateLimiter {
        private const string ShownKey     = "ps_last_shown_at";
        private const string DismissedKey = "ps_last_dismissed_at";

        private const double ShownCooldownDays     = 90;
        private const double DismissedCooldownDays = 7;

        internal static bool CanShow {
            get {
                var now = EpochNow();
                if (PlayerPrefs.HasKey(ShownKey)) {
                    var elapsed = now - PlayerPrefs.GetFloat(ShownKey);
                    if (elapsed < ShownCooldownDays * 86400) return false;
                }
                if (PlayerPrefs.HasKey(DismissedKey)) {
                    var elapsed = now - PlayerPrefs.GetFloat(DismissedKey);
                    if (elapsed < DismissedCooldownDays * 86400) return false;
                }
                return true;
            }
        }

        internal static void RecordShown() {
            PlayerPrefs.SetFloat(ShownKey, EpochNow());
            PlayerPrefs.Save();
        }

        internal static void RecordDismissed() {
            PlayerPrefs.SetFloat(DismissedKey, EpochNow());
            PlayerPrefs.Save();
        }

        private static float EpochNow() =>
            (float)(System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalSeconds;
    }
}
