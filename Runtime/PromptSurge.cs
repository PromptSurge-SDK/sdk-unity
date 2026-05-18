using PromptSurgeSDK.Internal;

namespace PromptSurgeSDK {
    /// <summary>Controls the verbosity of PromptSurge log output in the Unity console.</summary>
    public enum PromptSurgeLogLevel {
        /// <summary>No logging (default — recommended for production).</summary>
        None    = 0,
        /// <summary>Errors only.</summary>
        Error   = 1,
        /// <summary>Key lifecycle events (Initialize, RequestReview guards, dialog shown, button tapped, events sent).</summary>
        Info    = 2,
        /// <summary>Everything including cache hits and network details.</summary>
        Verbose = 3,
    }

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

        /// <summary>
        /// Set the SDK log level. Call before or after Initialize — order doesn't matter.
        /// Defaults to <see cref="PromptSurgeLogLevel.None"/> (silent in production).
        /// </summary>
        public static void SetLogLevel(PromptSurgeLogLevel level) {
            Internal.Logger.Level = (Internal.LogLevel)(int)level;
            Internal.Logger.Info($"Log level set to {level}.");
        }

        public static void Initialize(string apiKey,
                                      string apiBaseUrl = "https://api.promptsurge.me") {
            _apiKey      = apiKey;
            _apiBaseUrl  = apiBaseUrl;
            _initialized = true;
            Internal.Logger.Info($"Initialized — apiBaseUrl={apiBaseUrl}");
        }

        /// <summary>
        /// Opt this user out of all PromptSurge pre-prompt dialogs permanently (until OptIn is called).
        /// Persisted in PlayerPrefs across sessions. Safe to call before Initialize.
        /// </summary>
        public static void OptOut() {
            UnityEngine.PlayerPrefs.SetInt(OptOutKey, 1);
            UnityEngine.PlayerPrefs.Save();
            Internal.Logger.Info("User opted out.");
        }

        /// <summary>Re-enable pre-prompt dialogs after a previous OptOut call.</summary>
        public static void OptIn() {
            UnityEngine.PlayerPrefs.SetInt(OptOutKey, 0);
            UnityEngine.PlayerPrefs.Save();
            Internal.Logger.Info("User opted in.");
        }

        /// <summary>Whether the current user has opted out of review prompts.</summary>
        public static bool IsOptedOut =>
            UnityEngine.PlayerPrefs.GetInt(OptOutKey, 0) == 1;

        /// <summary>
        /// Warms the prompt cache in the background without showing any dialog.
        /// Call this early — e.g. immediately after Initialize() or at scene load —
        /// so the prompt text is already cached when RequestReview() is called at a
        /// high-satisfaction moment. Eliminates the network round-trip delay from
        /// the user-facing trigger point.
        ///
        /// Safe to call multiple times; no-ops if the cache is still fresh.
        /// </summary>
        public static void Prefetch() {
            if (!_initialized) {
                Internal.Logger.Error("Prefetch called before Initialize — ignoring.");
                return;
            }
            Internal.Logger.Info("Prefetching prompt…");
            PromptTextRepository.Fetch(_apiKey, _apiBaseUrl,
                onSuccess:       _ => Internal.Logger.Info("Prompt prefetch complete."),
                onLimitExceeded: () => Internal.Logger.Info("Prefetch: impression limit active (402)."));
        }

        /// <summary>
        /// Fetches the current prompt and shows the pre-prompt dialog if
        /// rate limits and holdout allow. Does nothing if not initialized.
        /// </summary>
        public static void RequestReview() {
            Internal.Logger.Info("RequestReview called.");

            if (!_initialized) {
                Internal.Logger.Error("RequestReview called before Initialize — ignoring.");
                return;
            }
            if (IsOptedOut) {
                Internal.Logger.Info("Skipping — user is opted out.");
                return;
            }
            if (HoldoutManager.IsHoldout) {
                Internal.Logger.Info("Holdout group — firing native review directly.");
                ReviewRequester.Request();
                Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.NativePromptRequested, null);
                RateLimiter.RecordShown();
                return;
            }
            if (!RateLimiter.CanShow) {
                Internal.Logger.Info("Skipping — rate limit not elapsed.");
                return;
            }

            PromptTextRepository.Fetch(_apiKey, _apiBaseUrl,
                onSuccess: response => {
                    var res = response ?? Defaults.Response;

                    RateLimiter.RecordShown();
                    Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.PrePromptShown,
                                   res.promptId, res.appPromptNumber > 0 ? res.appPromptNumber : (int?)null);

                    PrePromptCanvas.Show(res,
                        onAccept: () => {
                            Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.PrePromptConfirmed,
                                           res.promptId);
                            ReviewRequester.Request();
                            Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.NativePromptRequested);
                        },
                        onDismiss: () => {
                            RateLimiter.RecordDismissed();
                            Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.PrePromptDismissed,
                                           res.promptId);
                        });
                },
                onLimitExceeded: () => {
                    // Server billing limit hit — fire native review directly.
                    // No client-side caching: every call checks the server fresh.
                    Internal.Logger.Info("Impression limit exceeded (402) — firing native review directly.");
                    ReviewRequester.Request();
                    Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.NativePromptRequested, null);
                    RateLimiter.RecordShown();
                });
        }
    }
}
