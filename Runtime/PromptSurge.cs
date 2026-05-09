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

        // ── Public API ─────────────────────────────────────────────────────────

        public static void Initialize(string apiKey,
                                      string apiBaseUrl = "https://api.promptsurge.me") {
            _apiKey     = apiKey;
            _apiBaseUrl = apiBaseUrl;
            _initialized = true;
        }

        /// <summary>
        /// Fetches the current prompt and shows the pre-prompt dialog if
        /// rate limits and holdout allow. Does nothing in the editor.
        /// </summary>
        public static void RequestReview() {
            if (!_initialized) return;
            if (HoldoutManager.IsHoldout) return;
            if (!RateLimiter.CanShow) return;

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
