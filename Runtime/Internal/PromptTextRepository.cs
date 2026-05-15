using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace PromptSurgeSDK.Internal {
    internal static class PromptTextRepository {
        private const string CacheKey              = "ps_cached_prompt";
        private const string CacheTimeKey          = "ps_cached_prompt_at";
        private const string ImpressionLimitKey    = "ps_impression_limit_exceeded";
        private const float  CacheExpirySeconds    = 6 * 3600;

        internal static bool IsImpressionLimitExceeded =>
            PlayerPrefs.GetInt(ImpressionLimitKey, 0) == 1;

        internal static void Fetch(string apiKey, string apiBaseUrl, Action<PromptResponse> callback) {
            PromptSurgeRunner.Instance.StartCoroutine(FetchCoroutine(apiKey, apiBaseUrl, callback));
        }

        private static IEnumerator FetchCoroutine(string apiKey, string apiBaseUrl,
                                                   Action<PromptResponse> callback) {
            var cached = LoadCache();
            if (cached != null) {
                callback(cached);
                // Refresh in background
                PromptSurgeRunner.Instance.StartCoroutine(FetchAndCache(apiKey, apiBaseUrl, _ => { }));
                yield break;
            }
            yield return FetchAndCache(apiKey, apiBaseUrl, callback);
        }

        private static IEnumerator FetchAndCache(string apiKey, string apiBaseUrl,
                                                  Action<PromptResponse> callback) {
            var req = UnityWebRequest.Get(apiBaseUrl + "/v1/prompts");
            req.SetRequestHeader("X-PromptSurge-Key", apiKey);
            req.SetRequestHeader("Accept-Language", Application.systemLanguage.ToString());
            yield return req.SendWebRequest();

            PromptResponse result = null;
            if (req.responseCode == 402) {
                PlayerPrefs.SetInt(ImpressionLimitKey, 1);
                PlayerPrefs.Save();
            } else if (req.result == UnityWebRequest.Result.Success) {
                try {
                    result = JsonUtility.FromJson<PromptResponse>(req.downloadHandler.text);
                    SaveCache(req.downloadHandler.text);
                    PlayerPrefs.SetInt(ImpressionLimitKey, 0);
                    PlayerPrefs.Save();
                } catch { }
            }
            req.Dispose();
            callback(result);
        }

        private static PromptResponse LoadCache() {
            if (!PlayerPrefs.HasKey(CacheKey) || !PlayerPrefs.HasKey(CacheTimeKey))
                return null;
            var savedAt = PlayerPrefs.GetFloat(CacheTimeKey);
            var now = (float)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            if (now - savedAt > CacheExpirySeconds) return null;
            try {
                return JsonUtility.FromJson<PromptResponse>(PlayerPrefs.GetString(CacheKey));
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
