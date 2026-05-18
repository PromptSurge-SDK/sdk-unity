using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace PromptSurgeSDK.Internal {
    internal static class PromptTextRepository {
        private const string CacheKey           = "ps_cached_prompt";
        private const string CacheTimeKey       = "ps_cached_prompt_at";
        private const float  CacheExpirySeconds = 6 * 3600;

        /// <summary>
        /// Fetches the current prompt.
        /// On success, calls <paramref name="onSuccess"/> with the parsed response (may be null on parse error).
        /// On 402 (impression limit), calls <paramref name="onLimitExceeded"/> — the server is the single
        /// source of truth for billing limits; nothing is cached client-side for this signal.
        /// </summary>
        internal static void Fetch(string apiKey, string apiBaseUrl,
                                   Action<PromptResponse> onSuccess,
                                   Action onLimitExceeded = null) {
            PromptSurgeRunner.Instance.StartCoroutine(
                FetchCoroutine(apiKey, apiBaseUrl, onSuccess, onLimitExceeded));
        }

        private static IEnumerator FetchCoroutine(string apiKey, string apiBaseUrl,
                                                   Action<PromptResponse> onSuccess,
                                                   Action onLimitExceeded) {
            var cached = LoadCache();
            if (cached != null) {
                onSuccess(cached);
                // Refresh in background (ignore 402 during silent refresh)
                PromptSurgeRunner.Instance.StartCoroutine(
                    FetchAndCache(apiKey, apiBaseUrl, _ => { }, null));
                yield break;
            }
            yield return FetchAndCache(apiKey, apiBaseUrl, onSuccess, onLimitExceeded);
        }

        private static IEnumerator FetchAndCache(string apiKey, string apiBaseUrl,
                                                  Action<PromptResponse> onSuccess,
                                                  Action onLimitExceeded) {
            Logger.Info("Fetching prompt from API…");
            var req = UnityWebRequest.Get(apiBaseUrl + "/v1/prompts");
            req.SetRequestHeader("X-PromptSurge-Key", apiKey);
            req.SetRequestHeader("Accept-Language", Application.systemLanguage.ToString());
            yield return req.SendWebRequest();

            if (req.responseCode == 402) {
                Logger.Info("Impression limit reached (402) — server billing limit exceeded.");
                req.Dispose();
                onLimitExceeded?.Invoke();
                yield break;
            }

            PromptResponse result = null;
            if (req.result == UnityWebRequest.Result.Success) {
                try {
                    var api = JsonUtility.FromJson<APIPromptResponse>(req.downloadHandler.text);
                    result  = ApiMapper.Map(api);
                    SaveCache(req.downloadHandler.text);
                    Logger.Info($"Prompt fetched — id={result?.promptId} title=\"{result?.text?.title}\"");
                } catch (Exception ex) {
                    Logger.Error($"Failed to parse prompt response: {ex.Message}");
                }
            } else {
                Logger.Error($"Prompt fetch failed: {req.error} (HTTP {req.responseCode})");
            }
            req.Dispose();
            onSuccess(result);
        }

        private static PromptResponse LoadCache() {
            if (!PlayerPrefs.HasKey(CacheKey) || !PlayerPrefs.HasKey(CacheTimeKey))
                return null;
            var savedAt = PlayerPrefs.GetFloat(CacheTimeKey);
            var now = (float)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            if (now - savedAt > CacheExpirySeconds) return null;
            try {
                var api = JsonUtility.FromJson<APIPromptResponse>(PlayerPrefs.GetString(CacheKey));
                var cached = ApiMapper.Map(api);
                Logger.Info($"Using cached prompt — id={cached?.promptId}");
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
