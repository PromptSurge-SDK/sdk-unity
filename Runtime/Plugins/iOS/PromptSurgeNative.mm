#import <StoreKit/StoreKit.h>
#import <UIKit/UIKit.h>

// SKStoreReviewController on purpose, on every iOS version. Apple's newer
// AppStore.requestReview(in:) (iOS 16+) is a Swift-only API, and this shim is
// Objective-C++: reaching it would mean adding a Swift file to the plugin, which
// changes the Xcode export (Swift runtime, SWIFT_VERSION, bridging) of every game
// that embeds the SDK, and no Unity iOS build has been run to prove that safe.
// SKStoreReviewController still works on current iOS, and both APIs share the
// same limit of three displays in 365 days, which ReviewQuota.cs mirrors.

extern "C" {
    void _PS_RequestStoreReview() {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (@available(iOS 14.0, *)) {
                UIWindowScene *scene = nil;
                for (UIScene *s in UIApplication.sharedApplication.connectedScenes) {
                    if ([s isKindOfClass:[UIWindowScene class]] &&
                        s.activationState == UISceneActivationStateForegroundActive) {
                        scene = (UIWindowScene *)s;
                        break;
                    }
                }
                if (scene) {
                    [SKStoreReviewController requestReviewInScene:scene];
                }
            }
        });
    }
}
