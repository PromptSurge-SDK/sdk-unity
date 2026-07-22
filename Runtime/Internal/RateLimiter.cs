using System;
using System.Globalization;
using UnityEngine;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// End-user prompt cooldowns: 90 days after the pre-prompt is shown, 7 days after a dismissal.
    /// Same numbers as the Android and iOS SDKs (docs/conventions.md).
    ///
    /// From v1.0.8 to v1.0.14 <c>CanShow</c> was hardcoded to <c>true</c> for testing and shipped
    /// that way, so every player of every embedding game could be prompted on every call. The
    /// bypass now has to be asked for per device, and says so in the log when it is on.
    /// </summary>
    internal static class RateLimiter {
        // v2 keys: the originals held floats, whose ~7 significant digits give roughly two
        // minutes of resolution at present-day epoch seconds. Nothing is lost by starting
        // fresh - CanShow ignored these values for the whole life of the old keys.
        private const string ShownKey     = "ps_last_shown_at_v2";
        private const string DismissedKey = "ps_last_dismissed_at_v2";
        private const string BypassKey    = "ps_rate_limit_bypass";

        private const double ShownCooldownSeconds     = 90 * 24 * 3600;
        private const double DismissedCooldownSeconds = 7 * 24 * 3600;

        /// <summary>
        /// Debug escape hatch, off unless <see cref="PromptSurge.SetRateLimitBypass"/> was called
        /// on this device. Persisted, so QA sets it once. Never ship a build that calls the setter.
        /// </summary>
        internal static bool BypassEnabled {
            get => PlayerPrefs.GetInt(BypassKey, 0) == 1;
            set {
                PlayerPrefs.SetInt(BypassKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        internal static bool CanShow {
            get {
                if (BypassEnabled) {
                    Logger.Warn("Rate limiting is bypassed on this device (debug flag). " +
                                "Call PromptSurge.SetRateLimitBypass(false) to restore the 90/7 day cooldowns.");
                    return true;
                }

                var now = EpochNow();

                var shown = ReadStamp(ShownKey);
                if (shown > 0d && now - shown < ShownCooldownSeconds) {
                    Logger.Info($"Rate limited: pre-prompt shown {(now - shown) / 86400d:F1} days ago, cooldown is 90 days.");
                    return false;
                }

                var dismissed = ReadStamp(DismissedKey);
                if (dismissed > 0d && now - dismissed < DismissedCooldownSeconds) {
                    Logger.Info($"Rate limited: pre-prompt dismissed {(now - dismissed) / 86400d:F1} days ago, cooldown is 7 days.");
                    return false;
                }

                return true;
            }
        }

        internal static void RecordShown() => WriteStamp(ShownKey);

        internal static void RecordDismissed() => WriteStamp(DismissedKey);

        private static void WriteStamp(string key) {
            PlayerPrefs.SetString(key, EpochNow().ToString("F0", CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        /// Returns 0 when the key is absent or unparseable, which reads as "no cooldown recorded".
        private static double ReadStamp(string key) {
            var raw = PlayerPrefs.GetString(key, null);
            if (string.IsNullOrEmpty(raw)) return 0d;
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;
        }

        private static double EpochNow() =>
            (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }
}
