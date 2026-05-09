#import <StoreKit/StoreKit.h>
#import <UIKit/UIKit.h>

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
