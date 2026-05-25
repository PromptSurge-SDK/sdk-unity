using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PromptSurgeSDK.Internal {
    internal static class Telemetry {
        internal const string SdkVersion = "1.0.12";
        private static readonly string SessionId = Guid.NewGuid().ToString();

        internal static void Send(string apiKey, string apiBaseUrl, string eventType,
                                  string promptId = null, int? servedPromptNumber = null) {
            PromptSurgeRunner.Instance.StartCoroutine(
                PostEvent(apiKey, apiBaseUrl, eventType, promptId, servedPromptNumber));
        }

        private static IEnumerator PostEvent(string apiKey, string apiBaseUrl, string eventType,
                                             string promptId, int? servedPromptNumber) {
            // Build payload fields. servedPromptNumber must be an unquoted number (not a string).
            var payloadFields = "";
            if (promptId != null)
                payloadFields += $"\"promptId\":\"{promptId}\"";
            if (servedPromptNumber.HasValue)
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
    ""deviceId"":""{DeviceId()}"",
    ""appVersion"":""{appVersion}"",
    ""sdkVersion"":""{SdkVersion}"",
    ""locale"":""{Application.systemLanguage}"",
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
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success) {
                Logger.Verbose($"Event accepted: {eventType} (HTTP {req.responseCode})");
            } else {
                Logger.Error($"Event send failed: {eventType} — {req.error} (HTTP {req.responseCode})");
            }
            req.Dispose();
        }

        private static string DeviceId() {
            // SHA-256 is not available without extra packages; use SystemInfo.deviceUniqueIdentifier
            // which Unity already hashes on most platforms.
            return SystemInfo.deviceUniqueIdentifier;
        }

        private static string Platform() {
#if UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            return "android";
#else
            return "unity_editor";
#endif
        }
    }
}
