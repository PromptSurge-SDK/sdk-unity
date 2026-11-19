using System;
using UnityEngine;

namespace PromptSurgeSDK.Internal {
    // Invisible singleton MonoBehaviour that owns coroutines for the static SDK.
    internal class PromptSurgeRunner : MonoBehaviour {
        private static PromptSurgeRunner _instance;

        /// <summary>Time away after each rating-sheet request (card 833), fed from this object's focus events.</summary>
        internal static readonly SheetAwayTimer SheetAway = new SheetAwayTimer();

        internal static PromptSurgeRunner Instance {
            get {
                if (_instance != null) return _instance;
                var go = new GameObject("[PromptSurge]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<PromptSurgeRunner>();
                return _instance;
            }
        }

        /// <summary>Wall-clock seconds. Unlike frame time it keeps moving while the game is in the background.</summary>
        internal static double NowSeconds() {
            return DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
        }

        // The store's sheet takes focus from the game (and on Android usually pauses it); either
        // signal counts, and the first one back ends the measurement.
        private void OnApplicationFocus(bool hasFocus) {
            SheetAway.FocusChanged(hasFocus, NowSeconds());
        }

        private void OnApplicationPause(bool paused) {
            SheetAway.FocusChanged(!paused, NowSeconds());
        }

        private void Update() {
            if (SheetAway.Watching) SheetAway.Tick(NowSeconds());
        }

        private void OnDestroy() {
            if (_instance == this) _instance = null;
        }
    }
}
