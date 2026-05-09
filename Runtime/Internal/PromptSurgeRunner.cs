using UnityEngine;

namespace PromptSurgeSDK.Internal {
    // Invisible singleton MonoBehaviour that owns coroutines for the static SDK.
    internal class PromptSurgeRunner : MonoBehaviour {
        private static PromptSurgeRunner _instance;

        internal static PromptSurgeRunner Instance {
            get {
                if (_instance != null) return _instance;
                var go = new GameObject("[PromptSurge]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<PromptSurgeRunner>();
                return _instance;
            }
        }

        private void OnDestroy() {
            if (_instance == this) _instance = null;
        }
    }
}
