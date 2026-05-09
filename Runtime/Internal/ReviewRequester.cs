using UnityEngine;

namespace PromptSurgeSDK.Internal {
    internal static class ReviewRequester {
        internal static void Request() {
#if UNITY_IOS && !UNITY_EDITOR
            _PS_RequestStoreReview();
#elif UNITY_ANDROID && !UNITY_EDITOR
            RequestAndroid();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _PS_RequestStoreReview();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void RequestAndroid() {
            using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge   = new AndroidJavaClass("me.promptsurge.ReviewBridge");
            bridge.CallStatic("requestReview", activity);
        }
#endif
    }
}
