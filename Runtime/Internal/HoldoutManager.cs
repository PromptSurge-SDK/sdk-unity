using UnityEngine;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// Which A/B group this device is in: the holdout group never sees a pre-prompt, so its
    /// native review rate is the baseline the dashboard compares against.
    ///
    /// Card 349 phase 4: THE SERVER IS THE AUTHORITY FROM THE SECOND SESSION ONWARDS.
    /// PlayerPrefs do not survive an uninstall but the device id does, so a local-only coin flip
    /// re-rolls a reinstalling device into a new group and counts one device in both. POST
    /// /v1/events answers with the group the server remembers against that device id, and
    /// <see cref="AdoptFromServer"/> persists that answer over the local flip.
    ///
    /// The answer is applied to the NEXT session, never this one - see <see cref="IsHoldout"/>.
    /// Mirrors Android's Telemetry holdout block and iOS's HoldoutManager.
    /// </summary>
    internal static class HoldoutManager {
        private const string Key = "ps_holdout";

        /// <summary>
        /// Written the first time the server answers with an assignment, and never cleared. The
        /// SDK does not read it: it exists so a support session looking at a device's prefs can
        /// tell a locally flipped coin from the server's memory. Mirrors Android's
        /// `holdout_from_server`.
        /// </summary>
        private const string FromServerKey = "ps_holdout_from_server";

        /// <summary>
        /// Fixed for the life of the process, on purpose. The server's answer arrives on an event
        /// response, which can land at any moment; adopting it in place would let one session show
        /// the pre-prompt before that response and skip it afterwards, and the events either side
        /// would describe two different experiences under one session id. Caching here costs one
        /// session of staleness and keeps every session internally coherent.
        /// </summary>
        private static bool? _current;

        // Assigned once per device and cached. 10% probability.
        internal static bool IsHoldout {
            get {
                if (_current.HasValue) return _current.Value;

                if (PlayerPrefs.HasKey(Key)) {
                    _current = PlayerPrefs.GetInt(Key) == 1;
                    return _current.Value;
                }

                var assigned = Random.value < 0.10f;
                PlayerPrefs.SetInt(Key, assigned ? 1 : 0);
                PlayerPrefs.Save();
                _current = assigned;
                return assigned;
            }
        }

        /// <summary>
        /// Persists the group the SERVER remembers for this device. Deliberately does not touch
        /// <see cref="_current"/>: the running session keeps the group it was served under, and
        /// the next launch reads this value from PlayerPrefs.
        ///
        /// Writes only on a change, so a stable device stops touching the disk after the first
        /// answer.
        /// </summary>
        internal static void AdoptFromServer(bool holdout) {
            var alreadyFromServer = PlayerPrefs.GetInt(FromServerKey, 0) == 1;
            var stored = PlayerPrefs.HasKey(Key) && PlayerPrefs.GetInt(Key) == 1;
            if (alreadyFromServer && PlayerPrefs.HasKey(Key) && stored == holdout) return;

            PlayerPrefs.SetInt(Key, holdout ? 1 : 0);
            PlayerPrefs.SetInt(FromServerKey, 1);
            PlayerPrefs.Save();
            Logger.Verbose($"Holdout group adopted from server: {holdout} (applies next session)");
        }
    }
}
