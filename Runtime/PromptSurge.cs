using PromptSurgeSDK.Internal;

namespace PromptSurgeSDK {
    /// <summary>
    /// Entry point for the PromptSurge Unity SDK.
    /// Call Initialize() once at game start, then RequestReview() at a
    /// high-satisfaction moment (level complete, purchase success, etc.).
    ///
    /// In the Unity Editor every entry point is a no-op (docs/conventions.md): Play Mode must not
    /// generate billed impressions, and the eligibility checks must not write PlayerPrefs that
    /// then follow the developer around.
    /// </summary>
    public static class PromptSurge {
        private const string OptOutKey = "PromptSurge_OptedOut";

        // Declared only outside the editor: in an editor build every entry point returns before
        // touching them, and unused private fields are a warning in every consumer's console.
#if !UNITY_EDITOR
        private static string _apiKey;
        private static string _apiBaseUrl;
        private static bool   _initialized;
        private static bool   _requestInFlight;
#endif

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Set the SDK log level. Call before or after Initialize — order doesn't matter.
        /// Defaults to <see cref="LogLevel.None"/>, which still emits errors and warnings;
        /// the level only gates informational output.
        /// </summary>
        public static void SetLogLevel(LogLevel level) {
            Logger.Level = level;
            Logger.Info($"Log level set to {level}.");
        }

        /// <param name="verifyToken">Optional one-shot app-ownership token from the PromptSurge
        /// dashboard's Verify page. It rides along with the event batches and can be removed from
        /// your code once the dashboard shows the app as verified. Appended after apiBaseUrl so
        /// existing positional calls keep meaning what they meant; pass it by name:
        /// <c>PromptSurge.Initialize("ps_live_...", verifyToken: "vt_...")</c>.</param>
        public static void Initialize(string apiKey,
                                      string apiBaseUrl = "https://api.promptsurge.me",
                                      string verifyToken = null) {
#if UNITY_EDITOR
            Logger.Info("Initialize ignored in the Unity Editor — the SDK is a no-op here by design.");
            return;
#else
            var key = apiKey != null ? apiKey.Trim() : null;
            if (string.IsNullOrEmpty(key)) {
                Logger.Error("Initialize was called with an empty API key. The SDK is not active; " +
                             "RequestReview() will do nothing. Copy your key from https://admin.promptsurge.me.");
                _initialized = false;
                return;
            }
            if (key != apiKey) {
                Logger.Warn("The API key had surrounding whitespace, which has been trimmed.");
            }
            if (!key.StartsWith("ps_live_")) {
                Logger.Warn("The API key does not start with 'ps_live_'. Check you have not pasted an " +
                            "Android or iOS key, or an admin session token.");
            }

            _apiKey      = key;
            _apiBaseUrl  = apiBaseUrl;
            _initialized = true;
            // Set before FireLifecycleEvents so the token rides on the very first batch —
            // `initialize` is often the only event a brand-new install sends for a while.
            Telemetry.VerifyToken =
                string.IsNullOrWhiteSpace(verifyToken) ? null : verifyToken.Trim();
            Logger.Info($"Initialized — sdkVersion={Telemetry.SdkVersion} apiBaseUrl={apiBaseUrl}");

            // Fired here, after _initialized is set and inside the !UNITY_EDITOR
            // branch, so Play Mode stays a no-op exactly like every other call.
            // Same shape as Telemetry.fireLifecycleEvents() on iOS and
            // Telemetry.kt's KEY_FIRST_OPEN_FIRED on Android.
            Telemetry.FireLifecycleEvents(_apiKey, _apiBaseUrl);
#endif
        }

        /// <summary>
        /// Debug only. Disables the 90-day / 7-day cooldowns on this device so QA can trigger the
        /// dialog repeatedly. Persisted in PlayerPrefs, and every suppressed check logs a warning
        /// while it is on. Never call this from a build you ship.
        /// </summary>
        public static void SetRateLimitBypass(bool enabled) {
            RateLimiter.BypassEnabled = enabled;
            Logger.Warn(enabled
                ? "Rate limiting bypassed on this device. Call SetRateLimitBypass(false) before shipping."
                : "Rate limiting restored on this device.");
        }

        /// <summary>
        /// Opt this user out of all PromptSurge pre-prompt dialogs permanently (until OptIn is called).
        /// Persisted in PlayerPrefs across sessions. Safe to call before Initialize.
        /// </summary>
        public static void OptOut() {
            UnityEngine.PlayerPrefs.SetInt(OptOutKey, 1);
            UnityEngine.PlayerPrefs.Save();
            Logger.Info("User opted out.");
        }

        /// <summary>Re-enable pre-prompt dialogs after a previous OptOut call.</summary>
        public static void OptIn() {
            UnityEngine.PlayerPrefs.SetInt(OptOutKey, 0);
            UnityEngine.PlayerPrefs.Save();
            Logger.Info("User opted in.");
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
        /// Safe to call multiple times. A fresh cache is served immediately and revalidated in
        /// the background, so this always costs one request.
        /// </summary>
        public static void Prefetch() {
#if UNITY_EDITOR
            return;
#else
            if (!_initialized) {
                Logger.Error("Prefetch called before Initialize — ignoring.");
                return;
            }
            Logger.Info("Prefetching prompt…");
            PromptTextRepository.Fetch(_apiKey, _apiBaseUrl, (outcome, _) =>
                Logger.Info($"Prompt prefetch complete — outcome={outcome}."));
#endif
        }

        /// <summary>
        /// Fetches the current prompt and shows the pre-prompt dialog if
        /// rate limits and holdout allow. Does nothing if not initialized.
        /// </summary>
        public static void RequestReview() {
#if UNITY_EDITOR
            Logger.Info("RequestReview ignored in the Unity Editor — the SDK is a no-op here by design.");
            return;
#else
            Logger.Info("RequestReview called.");

            if (!_initialized) {
                Logger.Error("RequestReview called before Initialize — ignoring. " +
                             "Call PromptSurge.Initialize(apiKey) once at game start.");
                return;
            }
            if (IsOptedOut) {
                Logger.Info("Skipping — user is opted out.");
                return;
            }
            // Two calls used to stack two dialogs, each a full-screen DontDestroyOnLoad canvas at
            // sorting order 32767. The second one is unreachable behind the first.
            if (_requestInFlight) {
                Logger.Info("Skipping — a review request is already in flight.");
                return;
            }
            if (HoldoutManager.IsHoldout) {
                Logger.Info("Holdout group — firing native review directly.");
                FireNativeReview(recordCooldown: true);
                return;
            }
            if (!RateLimiter.CanShow) {
                return; // RateLimiter logs which cooldown is active.
            }

            _requestInFlight = true;
            PromptTextRepository.Fetch(_apiKey, _apiBaseUrl, (outcome, response) => {
                switch (outcome) {
                    case PromptFetchOutcome.LimitExceeded:
                        _requestInFlight = false;
                        FireNativeReview(recordCooldown: true);
                        return;

                    case PromptFetchOutcome.AppDeleted:
                        _requestInFlight = false;
                        FireNativeReview(recordCooldown: true);
                        return;

                    case PromptFetchOutcome.Unauthorized:
                        // A rejected key is a configuration bug, not a network blip. Showing the
                        // bundled English copy here is what made a broken key look like a working
                        // install; the error is already logged by the repository.
                        _requestInFlight = false;
                        FireNativeReview(recordCooldown: true);
                        return;
                }

                var res = response ?? Defaults.Response;

                // Warm-up phase: server signals the app hasn't yet accumulated
                // enough distinct-device events to enable pre-prompts. Fire native
                // review directly (same path as holdout) so the event is counted
                // toward the threshold. Never show the dialog during warm-up.
                if (res.warmup) {
                    Logger.Info("Warm-up phase — firing native review to build baseline.");
                    _requestInFlight = false;
                    FireNativeReview(recordCooldown: true);
                    return;
                }

                Show(res);
            });
#endif
        }

#if !UNITY_EDITOR
        private static void Show(PromptResponse res) {
            PrePromptCanvas.Show(res,
                onShown: () => {
                    // pre_prompt_shown is the billable unit, so it fires when the card is actually
                    // on screen — not on fetch success, before the prefab had even loaded.
                    _requestInFlight = false;
                    RateLimiter.RecordShown();
                    Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.PrePromptShown,
                                   res.promptId,
                                   res.appPromptNumber > 0 ? res.appPromptNumber : (int?)null,
                                   res.text?.locale);
                },
                onAccept: () => {
                    _requestInFlight = false;
                    Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.PrePromptConfirmed,
                                   res.promptId, null, res.text?.locale);
                    // The cooldown was already recorded when the dialog appeared.
                    FireNativeReview(recordCooldown: false);
                },
                onDismiss: () => {
                    _requestInFlight = false;
                    RateLimiter.RecordDismissed();
                    Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.PrePromptDismissed,
                                   res.promptId, null, res.text?.locale);
                },
                onFailed: () => {
                    // No dialog could be presented. Fall back to the native sheet rather than
                    // giving the player nothing; the presenter has already logged the reason.
                    _requestInFlight = false;
                    FireNativeReview(recordCooldown: true);
                });
        }

        private static void FireNativeReview(bool recordCooldown) {
            ReviewRequester.Request();
            Telemetry.Send(_apiKey, _apiBaseUrl, EventTypes.NativePromptRequested);
            if (recordCooldown) RateLimiter.RecordShown();
        }
#endif
    }
}
