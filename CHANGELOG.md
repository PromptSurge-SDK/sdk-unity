# Changelog - PromptSurge Unity SDK

The version is `version` in `package.json` (and `Telemetry.SdkVersion`, which must match). CI tags
the public mirror (`PromptSurge-SDK/sdk-unity`) with `v<version>` when it changes; tags are never
moved.

## 1.4.0 - unreleased (merging this branch tags it)

### Added

- **"Announce only" pre-prompt (card 833).** The SDK now tells the server it can draw a one-button
  notice (`notice=1` on `/v1/prompts`). When the app is set to "Announce only" in the PromptSurge
  console, the server answers `mode: "notice"`: the dismiss button (`button1`) is hidden, and the
  remaining button, the back button and a tap on the scrim all lead to the store's rating sheet; no
  `pre_prompt_dismissed` is sent. Apps left on the default "Ask first" see no change. The
  store-limit guard still decides first. The single button is centred: the button row's alignment
  is switched from right to middle for a notice (`DialogView.Bind`).
- **`native_sheet_closed { awayMs, source }`.** Neither store says whether its sheet appeared. After
  every native request the SDK times how long the game lost focus (`OnApplicationFocus` /
  `OnApplicationPause` on the `[PromptSurge]` runner); no focus loss within 3 seconds reports 0.
  New file `Runtime/Internal/SheetAwayTimer.cs` (pure C#).

### Changed

- **The device id is hashed like the native SDKs (card 625).** `deviceId` is now
  `SHA-256(SystemInfo.deviceUniqueIdentifier + Application.identifier)`, lowercase hex (was Unity's
  raw identifier: MD5(ANDROID_ID) on Android, the unhashed IDFV on iOS). Every device therefore
  arrives under a new id; so that no game's experiment is reset, the old value rides along ONCE per
  install as batch-level `legacyDeviceId` until a batch is accepted, and the server gives the new id
  the old id's holdout group. Needs the API from the same merge (older servers ignore the field).

### Check before tagging

- Built in a real Unity editor (2022.3 LTS) for Android and iOS, the notice shown with ONE button
  in the shipped prefab, centred under the text (a game with its own dialog prefab: the row that
  holds `button2` needs a Horizontal or Vertical Layout Group for the centring to apply), and a `native_sheet_closed` with a non-zero `awayMs` seen in the console after the
  store's sheet appeared on a device.

## 1.3.0 - 2026-10-19


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
