# Changelog - PromptSurge Unity SDK

The version is `version` in `package.json` (and `Telemetry.SdkVersion`, which must match). CI tags
the public mirror (`PromptSurge-SDK/sdk-unity`) with `v<version>` when it changes; tags are never
moved.

## Unreleased (proposed 1.3.0)

Not released. `package.json` and `Telemetry.SdkVersion` are still `1.2.0` and no tag has moved.

### Changed

- **The bundled default copy is a statement.** Title "A quick favor", body "A short review helps
  other people discover apps like this.", buttons "Sure" / "Not now" - identical on Unity, iOS,
  Android and the server's own fallback. It was "Leave a review?" / "Reviews help other people
  discover apps like this. Got a moment?". Google's in-app review guidance says an app should not
  ask the user any questions before or while presenting the rating button or card, and the old
  title was one; the no-satisfaction-question rule is unchanged. The default only shows when the
  server cannot be reached; dashboard copy replaces it.
- **Log lines use a plain hyphen where they had an em dash**, for example
  `Skipping - user is opted out.` and `Holdout group - firing native review directly.` If you match
  SDK log text anywhere, update the pattern.
- **Holdout devices now wait out the 90-day cooldown, as on iOS and Android.** `RequestReview()`
  used to test the holdout group before the cooldown, so a Unity control device asked the store on
  every call while iOS and Android control devices waited 90 days after a request; the same
  experiment measured a different thing on Unity. The order is now cooldown, store quota, holdout,
  then the pre-prompt, on all three SDKs. Expect fewer store requests from Unity control devices.
- `SetRateLimitBypass(true)` now also skips the store review quota check (with a warning), so a QA
  device can still trigger the dialog repeatedly.

### Added

- **Store review quota.** Neither store says whether a review request displayed anything, so a
  "Sure" could open nothing. The SDK now records every native review request it makes in
  `PlayerPrefs` (`ps_native_review_requests`, last 5 entries) - every path goes through
  `ReviewRequester.Request()`: the confirm path, holdout, warm-up, the impression limit, a deleted
  app, a rejected key and a dialog that could not be presented. Once the record says the quota is
  spent, `RequestReview()` shows **no pre-prompt and makes no native request**; no impression is
  recorded and one Info line names the date the next request becomes possible.
  - **iOS:** at most **3 requests in the last 365 days** (Apple shows the sheet at most 3 times a
    year per app per device).
  - **Android:** none **within 30 days** of the last one (Google: asking again within a short
    period, for example less than a month, may show nothing).
  - Holdout and warm-up devices are held to the same rule, so both sides of the lift comparison
    skip under the same condition.
  - No new telemetry event: a skipped call sends nothing, and `POST /v1/events` accepts only the
    six existing event types.
- README: recommended **App Review Information > Notes** text for App Store Connect.

### Not changed, on purpose

- **iOS stays on `SKStoreReviewController.requestReview(in:)`.** The native iOS SDK moved to
  `AppStore.requestReview(in:)` on iOS 16+, but that API is Swift-only and the Unity shim
  (`Runtime/Plugins/iOS/PromptSurgeNative.mm`) is Objective-C++. Adding Swift would change every
  game's Xcode export, and no Unity iOS build has run to prove it safe. Both APIs share the same
  limit.

### Also unreleased since 1.2.0

- Card 349: the SDK adopts the holdout group the server remembers for this device (from the
  `POST /v1/events` response), from the next session on. In the source since 2026-09-14, in no tag.

### Upgrade notes

- The request record starts empty on upgrade; requests made by 1.2.0 and earlier are not counted.
- Holdout devices on Unity have never been held to the 90-day cooldown (the holdout branch runs
  before it), so on Unity the quota is the first thing that limits how often a holdout device asks
  the store: at most 3 times a year on iOS and once a month on Android.
- The record cannot see review requests the game makes itself.
