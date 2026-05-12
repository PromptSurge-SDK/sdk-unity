using PromptSurgeSDK.Internal;

namespace PromptSurgeSDK {
    /// <summary>
    /// Entry point for the PromptSurge Unity SDK.
    /// Call Initialize() once at game start, then RequestReview() at a
    /// high-satisfaction moment (level complete, purchase success, etc.).
    /// </summary>
    public static class PromptSurge {
        private static string _apiKey;
        private static string _apiBaseUrl;
        private static bool   _initialized;
        private const  string OptOutKey = "PromptSurge_OptedOut";

        // ── Public API ─────────────────────────────────────────────────────────

        public static void Initialize(string apiKey,
                                      string apiBaseUrl = "https://api.promptsurge.me") {
            _apiKey     = apiKey;
            _apiBaseUrl = apiBaseUrl;
            _initialized = true;
        }

        /// <summary>
        /// Opt this user out of all PromptSurge pre-prompt dialogs permanently (until OptIn is called).
        /// Persisted in PlayerPrefs across sessions. Safe to call before Initialize.
        /// </summary>
        public static void OptOut() {
            UnityEngine.PlayerPrefs.SetInt(OptOutKey, 1);
            UnityEngine.PlayerPrefs.Save();
        }

        /// <summary>Re-enable pre-prompt dialogs after a previous OptOut call.</summary>
        public static void OptIn() {
            UnityEngine.PlayerPrefs.SetInt(OptOutKey, 0);
            UnityEngine.PlayerPrefs.Save();
        }

        /// <summary>Whether the current user has opted out of review prompts.</summary>
        public static bool IsOptedOut =>
            UnityEngine.PlayerPrefs.GetInt(OptOutKey, 0) == 1;

        /// <summary>
        /// Fetches the current prompt and shows the pre-prompt dialog if
        /// rate limits and holdout allow. Does nothing in the editor.
        /// </summary>
        public static void RequestReview() {
            if (!_initialized) return;
            if (IsOptedOut) return;
            if (HoldoutManager.IsHoldout) return;
            if (!RateLimiter.CanShow) return;

            // Impression limit reached — skip pre-prompt, fire native review directly.
            if (PromptTextRepository.IsImpressionLimitExceeded) {
                ReviewRequester.Request();
                Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.ReviewRequested, null);
                RateLimiter.RecordShown();
                return;
            }

            PromptTextRepository.Fetch(_apiKey, _apiBaseUrl, response => {
                var res = response ?? Defaults.Response;

                RateLimiter.RecordShown();
                Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.PrePromptShown,
                               res.promptId, res.appPromptNumber > 0 ? res.appPromptNumber : (int?)null);

                PrePromptCanvas.Show(res,
                    onAccept: () => {
                        Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.PrePromptAccepted,
                                       res.promptId);
                        ReviewRequester.Request();
                        Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.ReviewRequested);
                    },
                    onDismiss: () => {
                        RateLimiter.RecordDismissed();
                        Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.PrePromptDismissed,
                                       res.promptId);
                    });
            });
        }
    }
}
