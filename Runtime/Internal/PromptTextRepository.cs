using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace PromptSurgeSDK.Internal {
    /// <summary>Every branch the server can take, so a caller cannot confuse "no" with "yes".</summary>
    internal enum PromptFetchOutcome {
        /// Copy to render, from the cache or from the network.
        Prompt,
        /// 402 — over the monthly impression limit for the current tier.
        LimitExceeded,
        /// 404 app_deleted — the app was deleted in the admin panel.
        AppDeleted,
        /// 401/403 — the API key is missing, wrong, or revoked. Never show a dialog for this.
        Unauthorized,
        /// Network failure, unparseable body, or an unexpected status.
        Unavailable,
    }

    internal static class PromptTextRepository {
        private const string CacheKey           = "ps_cached_prompt";
        private const string CacheTimeKey       = "ps_cached_prompt_at";
        private const string LimitKey           = "ps_impression_limit_exceeded";
        private const string DeletedKey         = "ps_app_deleted";
        private const float  CacheExpirySeconds = 6 * 3600;
        private const int    TimeoutSeconds     = 10;

        /// <summary>The server told us this app was deleted. Persisted, cleared by the next 200.</summary>
        internal static bool IsAppDeleted => PlayerPrefs.GetInt(DeletedKey, 0) == 1;

        /// <summary>
        /// The server told us the monthly impression limit is spent. Persisted, cleared by the
        /// next 200 — which is how it recovers when the billing period rolls over.
        /// </summary>
        internal static bool IsImpressionLimitExceeded => PlayerPrefs.GetInt(LimitKey, 0) == 1;

        /// <summary>
        /// Fetches the current prompt and reports which branch the server took.
        /// <paramref name="onResult"/> receives a non-null response only for
        /// <see cref="PromptFetchOutcome.Prompt"/>.
        /// </summary>
        internal static void Fetch(string apiKey, string apiBaseUrl,
                                   Action<PromptFetchOutcome, PromptResponse> onResult) {
            PromptSurgeRunner.Instance.StartCoroutine(
                FetchCoroutine(apiKey, apiBaseUrl, onResult));
        }

        private static IEnumerator FetchCoroutine(string apiKey, string apiBaseUrl,
                                                  Action<PromptFetchOutcome, PromptResponse> onResult) {
            // Both flags are hard suppressions, so a warm cache must never satisfy the call while
            // one is set: that let billing overshoot for up to the full six-hour cache lifetime
            // after the limit was hit, and kept serving a dialog for an app that no longer exists.
            if (IsAppDeleted || IsImpressionLimitExceeded) {
                var lastVerdict = IsAppDeleted ? PromptFetchOutcome.AppDeleted : PromptFetchOutcome.LimitExceeded;
                yield return FetchAndCache(apiKey, apiBaseUrl, (outcome, response) => {
                    // Offline: keep honouring the last verdict rather than falling back to a
                    // dialog the server has already told us not to show.
                    onResult(outcome == PromptFetchOutcome.Unavailable ? lastVerdict : outcome, response);
                });
                yield break;
            }

            var cached = LoadCache();
            if (cached != null) {
                onResult(PromptFetchOutcome.Prompt, cached);
                // Silent refresh so the cache, and both suppression flags, stay current.
                PromptSurgeRunner.Instance.StartCoroutine(
                    FetchAndCache(apiKey, apiBaseUrl, (_, __) => { }));
                yield break;
            }

            yield return FetchAndCache(apiKey, apiBaseUrl, onResult);
        }

        private static IEnumerator FetchAndCache(string apiKey, string apiBaseUrl,
                                                 Action<PromptFetchOutcome, PromptResponse> onResult) {
            Logger.Info("Fetching prompt from API…");
            // appVersion drives per-version warm-up buckets server-side. Match the value
            // the SDK reports on events so device counts line up. Older servers ignore it.
            var version = string.IsNullOrEmpty(Application.version) ? "unknown" : Application.version;
            // The server reads the `locale` query parameter and nothing else — the
            // Accept-Language header this used to send was never inspected by the route.
            var url = apiBaseUrl + "/v1/prompts"
                      + "?locale="     + UnityWebRequest.EscapeURL(LocaleTag.Current())
                      + "&sessionId="  + UnityWebRequest.EscapeURL(Telemetry.SessionId)
                      + "&appVersion=" + UnityWebRequest.EscapeURL(version);

            var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("X-PromptSurge-Key", apiKey);
            // Without this the request has no timeout at all, so a captive portal or a half-open
            // socket leaves the caller waiting on the OS TCP timeout.
            req.timeout = TimeoutSeconds;
            yield return req.SendWebRequest();

            var status = req.responseCode;
            var body   = req.downloadHandler != null ? req.downloadHandler.text : null;
            var failed = req.result != UnityWebRequest.Result.Success;
            var error  = req.error;
            req.Dispose();

            switch (status) {
                case 401:
                case 403:
                    Logger.Error($"API key rejected (HTTP {status}). Check the key passed to " +
                                 "PromptSurge.Initialize() against the one in the admin panel at " +
                                 "https://admin.promptsurge.me — it should start with 'ps_live_'. " +
                                 "No pre-prompt will be shown until this is fixed.");
                    onResult(PromptFetchOutcome.Unauthorized, null);
                    yield break;

                case 402:
                    SetFlag(LimitKey, true);
                    Logger.Warn("Monthly impression limit reached — the native review prompt fires directly. " +
                                "See https://admin.promptsurge.me/billing");
                    onResult(PromptFetchOutcome.LimitExceeded, null);
                    yield break;

                case 404:
                    if (IndicatesAppDeleted(body)) {
                        SetFlag(DeletedKey, true);
                        Logger.Warn("This app was deleted in the PromptSurge admin panel; the pre-prompt is suppressed.");
                        onResult(PromptFetchOutcome.AppDeleted, null);
                    } else {
                        Logger.Warn($"Prompt endpoint returned 404 for {url} — check the apiBaseUrl.");
                        onResult(PromptFetchOutcome.Unavailable, null);
                    }
                    yield break;
            }

            if (failed || status < 200 || status >= 300) {
                Logger.Warn($"Prompt fetch failed: {error} (HTTP {status})");
                onResult(PromptFetchOutcome.Unavailable, null);
                yield break;
            }

            APIPromptResponse api = null;
            try {
                api = JsonUtility.FromJson<APIPromptResponse>(body);
            } catch (Exception ex) {
                Logger.Error($"Failed to parse prompt response: {ex.Message}");
            }

            // JsonUtility does not throw on a shape mismatch, it just leaves every field at its
            // default — so an error body decodes into an empty "prompt". Check the copy is there.
            if (!ApiMapper.LooksLikePrompt(api)) {
                Logger.Error("Prompt response was missing its copy fields and was discarded. " +
                             "The server returned something that is not a prompt.");
                onResult(PromptFetchOutcome.Unavailable, null);
                yield break;
            }

            // A 200 supersedes both suppression flags — this is how an app recovers from a
            // transient 404 during a deploy, and from a limit that reset with the billing period.
            SetFlag(LimitKey, false);
            SetFlag(DeletedKey, false);

            var result = ApiMapper.Map(api);
            // Do not cache warm-up responses — they must always reflect live server state
            // so the counter can advance and warm-up completion is detected promptly.
            if (!api.warmup) SaveCache(body);
            Logger.Info($"Prompt fetched — warmup={api.warmup} locale={result?.text?.locale} title=\"{result?.text?.title}\"");
            onResult(PromptFetchOutcome.Prompt, result);
        }

        /// Matches the parsed `error` field rather than searching the raw body, so an unrelated
        /// 404 page that happens to contain the words cannot disable the SDK.
        private static bool IndicatesAppDeleted(string body) {
            if (string.IsNullOrEmpty(body)) return false;
            try {
                var envelope = JsonUtility.FromJson<APIErrorResponse>(body);
                return envelope != null && envelope.error == "app_deleted";
            } catch {
                return false;
            }
        }

        private static void SetFlag(string key, bool value) {
            var current = PlayerPrefs.GetInt(key, 0) == 1;
            if (current == value) return;
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static PromptResponse LoadCache() {
            if (!PlayerPrefs.HasKey(CacheKey) || !PlayerPrefs.HasKey(CacheTimeKey))
                return null;
            var savedAt = PlayerPrefs.GetFloat(CacheTimeKey);
            var now = (float)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            if (now - savedAt > CacheExpirySeconds) return null;
            try {
                var api = JsonUtility.FromJson<APIPromptResponse>(PlayerPrefs.GetString(CacheKey));
                if (!ApiMapper.LooksLikePrompt(api)) return null;
                var cached = ApiMapper.Map(api);
                Logger.Info($"Using cached prompt — locale={cached?.text?.locale}");
                return cached;
            } catch {
                return null;
            }
        }

        private static void SaveCache(string json) {
            var now = (float)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            PlayerPrefs.SetString(CacheKey, json);
            PlayerPrefs.SetFloat(CacheTimeKey, now);
            PlayerPrefs.Save();
        }
    }
}
