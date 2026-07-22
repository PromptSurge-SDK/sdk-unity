using System;
using UnityEngine;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// Attached to the instantiated dialog root. Handles the Android hardware back button, which
    /// Unity surfaces as <c>KeyCode.Escape</c>.
    ///
    /// Without this, the only way out of the dialog was its two buttons — so any state in which
    /// those buttons could not be pressed left a full-screen input-blocking canvas at sorting
    /// order 32767 that the player could not escape.
    /// </summary>
    internal class DialogDismissWatcher : MonoBehaviour {
        internal Action OnBackPressed;

        /// Set once the dialog is actually visible. Before that the back button belongs to the game.
        internal bool Armed;

        private void Update() {
            if (!Armed) return;
            if (!BackPressed()) return;

            Armed = false;
            var handler = OnBackPressed;
            OnBackPressed = null;
            handler?.Invoke();
        }

        private static bool BackPressed() {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            // Input System only: reading UnityEngine.Input throws. Reaching into the package by
            // reflection every frame is not worth it — the scrim tap remains the way out, and the
            // host's own back handling is untouched.
            return false;
#endif
        }

#if !ENABLE_LEGACY_INPUT_MANAGER
        private void Awake() {
            Logger.Info("Back-button dismissal is unavailable under Input System only input handling; " +
                        "tapping outside the card still dismisses.");
        }
#endif
    }
}
