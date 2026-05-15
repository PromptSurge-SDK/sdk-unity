using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PromptSurgeSDK.Internal {
    internal static class Telemetry {
        internal const string SdkVersion = "1.0.0";
        private static readonly string SessionId = Guid.NewGuid().ToString();

        internal static void Send(string apiKey, string apiBaseUrl, string eventType,
                                  string promptId = null, int? servedPromptNumber = null) {
            PromptSurgeRunner.Instance.StartCoroutine(
                PostEvent(apiKey, apiBaseUrl, eventType, promptId, servedPromptNumber));
        }

        private static IEnumerator PostEvent(string apiKey, string apiBaseUrl, string eventType,
                                             string promptId, int? servedPromptNumber) {
            var payload = promptId != null
                ? $"\"promptId\":\"{promptId}\""
                : "";
            if (servedPromptNumber.HasValue)
                payload += (payload.Length > 0 ? "," : "") +
                           $"\"servedPromptNumber\":\"{servedPromptNumber}\"";

            var json = $@"{{
  ""eventType"":""{eventType}"",
  ""eventId"":""{Guid.NewGuid()}"",
  ""timestamp"":""{DateTime.UtcNow:O}"",
  ""sessionId"":""{SessionId}"",
  ""deviceId"":""{DeviceId()}"",
  ""appVersion"":""{Application.version}"",
  ""sdkVersion"":""{SdkVersion}"",
  ""locale"":""{Application.systemLanguage}"",
  ""platform"":""{Platform()}"",
  ""holdout"":{(HoldoutManager.IsHoldout ? "true" : "false")},
  ""payload"":{{{payload}}}
}}";

            var req = new UnityWebRequest(apiBaseUrl + "/v1/events", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("X-PromptSurge-Key", apiKey);
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
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
