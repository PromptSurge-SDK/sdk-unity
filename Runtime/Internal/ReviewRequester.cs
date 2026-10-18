using UnityEngine;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// The one place the SDK reaches the native review API. Every native request goes through
    /// here - the confirm path, holdout, warm-up, the impression limit, a deleted app, a rejected
    /// key and a dialog that could not be presented - so <see cref="ReviewQuota"/> cannot miss one.
    ///
    /// iOS goes through <c>SKStoreReviewController</c> in Plugins/iOS/PromptSurgeNative.mm, not
    /// <c>AppStore.requestReview(in:)</c>: the newer API is Swift-only and the shim is
    /// Objective-C++. Both are held to the same three-in-365-days limit.
    /// </summary>
    internal static class ReviewRequester {
        internal static void Request() {
            // Recorded when the SDK asks, whether or not the store then shows anything: neither
            // store says, so the count of requests is all the quota check can go on.
            ReviewQuota.RecordRequest();
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
