using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PromptSurgeSDK.Internal {
    internal static class Telemetry {
        internal const string SdkVersion = "1.1.1";
        private const int TimeoutSeconds = 10;

        /// Shared with the prompt fetch so the server can select a copy variant deterministically
        /// and the resulting events attribute to that same session.
        internal static readonly string SessionId = Guid.NewGuid().ToString();

        /// PlayerPrefs key marking that `first_open` has already been sent on this
        /// install. Mirrors iOS's `ps_first_open_fired` and Android's
        /// KEY_FIRST_OPEN_FIRED, deliberately including the name, so the three
        /// platforms are greppable as one thing.
        private const string FirstOpenKey = "ps_first_open_fired";

        /// Fires `initialize` (every launch) and `first_open` (once per install).
        ///
        /// Order matters: the flag is written BEFORE the event is sent, so a send
        /// that fails cannot re-fire first_open on the next launch and inflate
        /// installs. Losing one first_open is a small undercount; double-counting
        /// every user with a flaky network is a wrong number that looks fine.
        internal static void FireLifecycleEvents(string apiKey, string apiBaseUrl) {
            Send(apiKey, apiBaseUrl, EventTypes.Initialize);
            if (PlayerPrefs.GetInt(FirstOpenKey, 0) == 1) return;
            PlayerPrefs.SetInt(FirstOpenKey, 1);
            PlayerPrefs.Save();
            Send(apiKey, apiBaseUrl, EventTypes.FirstOpen);
        }

        internal static void Send(string apiKey, string apiBaseUrl, string eventType,
                                  string promptId = null, int? servedPromptNumber = null,
                                  string resolvedLocale = null) {
            PromptSurgeRunner.Instance.StartCoroutine(
                PostEvent(apiKey, apiBaseUrl, eventType, promptId, servedPromptNumber, resolvedLocale));
        }

        private static IEnumerator PostEvent(string apiKey, string apiBaseUrl, string eventType,
                                             string promptId, int? servedPromptNumber,
                                             string resolvedLocale) {
            // Build payload fields. servedPromptNumber must be an unquoted number (not a string).
            var payloadFields = "";
            if (!string.IsNullOrEmpty(promptId))
                payloadFields += $"\"promptId\":\"{Escape(promptId)}\"";
            if (!string.IsNullOrEmpty(resolvedLocale))
                payloadFields += (payloadFields.Length > 0 ? "," : "") +
                                 $"\"resolvedLocale\":\"{Escape(resolvedLocale)}\"";
            if (servedPromptNumber.HasValue && servedPromptNumber.Value > 0)
                payloadFields += (payloadFields.Length > 0 ? "," : "") +
                                 $"\"servedPromptNumber\":{servedPromptNumber.Value}";

            // Application.version can be empty in editor builds — fall back to "0.0.0" so the
            // schema's min(1) check doesn't reject the event.
            var appVersion = string.IsNullOrEmpty(Application.version) ? "0.0.0" : Application.version;

            // Wrap in a batch envelope: the server expects { "events": [...] }.
            var json = $@"{{
  ""events"":[{{
    ""eventType"":""{eventType}"",
    ""eventId"":""{Guid.NewGuid()}"",
    ""timestamp"":""{DateTime.UtcNow:O}"",
    ""sessionId"":""{SessionId}"",
    ""deviceId"":""{Escape(DeviceId())}"",
    ""appVersion"":""{Escape(appVersion)}"",
    ""sdkVersion"":""{SdkVersion}"",
    ""locale"":""{Escape(LocaleTag.Current())}"",
    ""platform"":""{Platform()}"",
    ""holdout"":{(HoldoutManager.IsHoldout ? "true" : "false")},
    ""payload"":{{{payloadFields}}}
  }}]
}}";

            Logger.Info($"Sending event: {eventType}" +
                        (promptId != null ? $" promptId={promptId}" : ""));

            var req = new UnityWebRequest(apiBaseUrl + "/v1/events", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("X-PromptSurge-Key", apiKey);
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = TimeoutSeconds;
            yield return req.SendWebRequest();

            var status = req.responseCode;
            var success = req.result == UnityWebRequest.Result.Success;
            var error = req.error;
            req.Dispose();

            if (success) {
                Logger.Verbose($"Event accepted: {eventType} (HTTP {status})");
            } else if (status == 401 || status == 403) {
                Logger.Error($"Event '{eventType}' rejected: the API key is not valid (HTTP {status}).");
            } else if (status == 400) {
                Logger.Error($"Event '{eventType}' rejected as malformed (HTTP 400) — the SDK and server event schemas disagree.");
            } else {
                Logger.Warn($"Event send failed: {eventType} — {error} (HTTP {status})");
            }
        }

        private static string DeviceId() {
            // SHA-256 is not available without extra packages; use SystemInfo.deviceUniqueIdentifier
            // which Unity already hashes on most platforms.
            return SystemInfo.deviceUniqueIdentifier;
        }

        /// <summary>
        /// The editor check has to come first. With a bare <c>#if UNITY_IOS</c>, an editor session
        /// on the iOS build target reported <c>platform: "ios"</c>, so every Play Mode run showed
        /// up in the dashboard as real iOS traffic.
        /// </summary>
        private static string Platform() {
#if UNITY_EDITOR
            return "unity_editor";
#elif UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            return "android";
#else
            return "unity_editor";
#endif
        }

        /// Minimal JSON string escaping. The event JSON is hand-built, and a device id or an app
        /// version with a quote or a backslash in it would otherwise produce a 400 for the batch.
        private static string Escape(string value) {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
