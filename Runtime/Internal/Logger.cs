using UnityEngine;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// Internal logger. <see cref="Level"/> is set via <see cref="PromptSurge.SetLogLevel"/> and
    /// controls informational output only.
    ///
    /// Errors and warnings are deliberately NOT gated by the level. <c>Level</c> defaults to
    /// <see cref="LogLevel.None"/>, so gating them meant a rejected API key, a failed fetch and a
    /// missing prefab all produced complete silence in every shipped build — which is how a
    /// misconfigured key came to look like a working install.
    /// </summary>
    internal static class Logger {
        internal static LogLevel Level = LogLevel.None;
        private const string Tag = "[PromptSurge]";

        internal static void Error(string msg) {
            Debug.LogError($"{Tag} {msg}");
        }

        internal static void Warn(string msg) {
            Debug.LogWarning($"{Tag} {msg}");
        }

        internal static void Info(string msg) {
            if (Level >= LogLevel.Info) Debug.Log($"{Tag} {msg}");
        }

        internal static void Verbose(string msg) {
            if (Level >= LogLevel.Verbose) Debug.Log($"{Tag} {msg}");
        }
    }
}
