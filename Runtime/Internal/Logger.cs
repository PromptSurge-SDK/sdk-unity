using UnityEngine;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// Internal logger. Level is set via <see cref="PromptSurge.SetLogLevel"/>.
    /// </summary>
    internal static class Logger {
        internal static LogLevel Level = LogLevel.None;
        private const string Tag = "[PromptSurge]";

        internal static void Error(string msg) {
            if (Level >= LogLevel.Error) Debug.LogError($"{Tag} {msg}");
        }

        internal static void Info(string msg) {
            if (Level >= LogLevel.Info) Debug.Log($"{Tag} {msg}");
        }

        internal static void Verbose(string msg) {
            if (Level >= LogLevel.Verbose) Debug.Log($"{Tag} {msg}");
        }
    }

    /// <summary>Internal mirror of the public <see cref="PromptSurgeLogLevel"/> enum.</summary>
    internal enum LogLevel {
        None    = 0,
        Error   = 1,
        Info    = 2,
        Verbose = 3,
    }
}
