# PromptSurge Unity SDK

Unified Android + iOS review prompt SDK for Unity 2022.3+. Shows a pre-prompt dialog before triggering the native OS review sheet, increasing tap-through rates.

## Requirements

- Unity 2022.3 LTS or later. The package is pure C# with native shims and has no known 2022.3-only
  API in it, so older Unity versions may well work - but none has been compiled and they are
  unsupported. If you need an older LTS, open an issue and say which one.
- Android: Play Store distribution, `minSdkVersion 21`
- iOS: iOS 14+ deployment target

## Installation

### Option A - Unity Package Manager (Git URL)

In Unity: **Window → Package Manager → + → Add package from git URL**

```
https://github.com/PromptSurge-SDK/sdk-unity.git
```

Pin a version by appending `#v1.3.0`. Without a tag the Package Manager tracks the default branch, which moves.

### Option B - Local tarball

Run `npm pack` in this folder (or in a checkout of the mirror at the tag you want) and use **Add package from tarball** on the `.tgz` it writes.

## Android dependency

The SDK uses Google Play's In-App Review API. Add the dependency to your project one of two ways:

**With [External Dependency Manager (EDM4U)](https://github.com/googlesamples/unity-jar-resolver):**  
The included `Editor/PromptSurgeDependencies.xml` is picked up automatically. No manual step needed.

**Without EDM4U** - add to `Assets/Plugins/Android/mainTemplate.gradle`:
```groovy
dependencies {
    implementation 'com.google.android.play:review:2.0.1'
}
```

## Usage

```csharp
// GameManager.cs (runs once at startup)
using PromptSurgeSDK;

void Start() {
    PromptSurge.Initialize("ps_live_your_key_here");

    // Optional, while integrating: one line per decision the SDK makes.
    PromptSurge.SetLogLevel(LogLevel.Info);
}

// After a level complete, purchase, or any high-satisfaction event
void OnLevelComplete() {
    PromptSurge.RequestReview();
}
```

### Verifying app ownership (SDK 1.2.0+)

The dashboard's Verify page hands you a one-shot token that proves this store listing is
yours. Pass it by name once at initialisation (it is deliberately the last parameter, so
existing positional calls keep working):

```csharp
PromptSurge.Initialize("ps_live_your_key_here", verifyToken: "vt_xxxx");
```

It rides along with the event batches; remove it from your code once the dashboard shows the
app as verified - it has no effect after that.

## Diagnostics

Errors and warnings always print, whatever the log level: a rejected API key, a failed fetch and a missing prefab each say so by name.

**A suppressed dialog usually does not.** The two server-side suppressions - impression cap and deleted app - are warnings and always print. But warm-up, holdout, both cooldowns, the store review quota and opted-out are logged at **Info**, and `LogLevel.None` is the default. Those cover every reason a correctly wired integration shows nothing, so the most confusing case is also the quietest one:

```csharp
PromptSurge.SetLogLevel(LogLevel.Info);
```

Do that first, before anything else in this section. `LogLevel.Verbose` adds per-event delivery results. Filter the console for `[PromptSurge]`.

If nothing appears at all in a device build, `Initialize` was never called.

### Nothing is showing and I do not know why

With `LogLevel.Info` set, the SDK prints exactly which of these it hit, in this order. Without it, all of them are silent.

| What you will see | What it means |
|---|---|
| *(nothing at all, even at Info)* | `Initialize` was never called, or you are in the **Editor** - the SDK is a deliberate no-op there. Build to a device. |
| `Initialize was called with an empty API key...` (error) | Always prints. The SDK is not active. |
| `The API key does not start with 'ps_live_'...` (warning) | Always prints. Probably a test key or a key from another platform. |
| `Skipping - user is opted out.` | `OptOut()` was called at some point; it persists until `OptIn()`. |
| `Warm-up phase - firing native review to build baseline.` | **The one that catches every new integration.** See Behaviour below. |
| `Holdout group - firing native review directly.` | This device is in the 10% control group, for its lifetime. Try another device. |
| `Rate limited: pre-prompt shown N days ago, cooldown is 90 days.` | Already shown on this device. `SetRateLimitBypass(true)` lifts it on a QA device. |
| `Rate limited: pre-prompt dismissed N days ago, cooldown is 7 days.` | Dismissed on this device. Same bypass. |
| `Skipping - the SDK has already asked iOS for the rating sheet 3 times in the last 365 days...` | iOS: the App Store review quota is spent on this device (see Behaviour). The line ends with the date the next request becomes possible. |
| `Skipping - the SDK already asked Google Play for the review flow in the last 30 days...` | Android: the same for Play's quota. |
| `Skipping - a review request is already in flight.` | Two calls raced; harmless. |
| `Monthly impression limit reached...` (warning) | Always prints. Plan cap spent; clears when the billing period rolls over. |
| `This app was deleted in the PromptSurge admin panel...` (warning) | Always prints. Restore the app to clear it. |

In the warm-up, holdout, impression-limit and deleted-app cases the **native** review sheet is still requested, so a missing pre-prompt does not mean nothing happened; the store itself decides whether that sheet appears. In the review-quota case nothing is requested at all, on purpose.

## Behaviour

- **Warm-up phase:** a brand-new app shows **no pre-prompt at all** until it has recorded **50 distinct devices** firing `native_prompt_requested`. Until then every `RequestReview()` fires the native store review directly, which is what builds the baseline the whole product measures lift against. Default mode is `once` - one warm-up for the app's lifetime, not per release. **A test device will never reach 50 on its own**, so this is the expected state during an integration, and it is why "the dialog never appears" is usually not a bug. Turn it off for an app from its overview page in the dashboard (Warm-up control), or leave it and test with `LogLevel.Info` so you can see it happening.
- **Holdout group:** 10% of devices silently skipped (stored in `PlayerPrefs`).
- **Rate limiting:** 90-day cooldown after shown; 7-day after dismiss. The cooldown is recorded when the dialog actually appears, not when it is requested.
- **Store review quota:** neither store tells a game whether a review request displayed anything, so a "Sure" could be answered by nothing - which is what both stores warn about when an app puts a button in front of the review API. The SDK records every native request it makes (the confirm path, holdout, warm-up, the impression limit) in `PlayerPrefs`, and skips **both the pre-prompt and the native request** on a `RequestReview()` call once that record says the quota is spent. No impression is recorded, and one Info line says why and when the next request becomes possible.
  - **iOS:** Apple shows the rating sheet at most 3 times in 365 days per app per device, so the SDK stops at **3 requests in the last 365 days** and asks again once the oldest is a year old.
  - **Android:** Google does not publish its quota; its guide says asking again within a short period (for example, less than a month) may show nothing, so the SDK stops for **30 days after a request**.
  - Holdout and warm-up devices are held to the same rule, so both sides of the lift comparison skip under the same condition. The record cannot see review requests your game makes itself; if you have your own, they share the store's budget.
- **Impression limit:** When your plan's monthly cap is reached the API returns `402`. The SDK persists this in `PlayerPrefs`, suppresses the dialog and fires the native OS review sheet directly. The flag clears on the next successful response, i.e. when the billing period rolls over.
- **Deleted apps:** if the app is deleted in the admin panel the API returns `404 app_deleted` and the pre-prompt is suppressed while the native sheet still fires. Restoring the app clears the flag.
- **Invalid API key:** `401`/`403` is logged as an error and no pre-prompt is shown. The SDK deliberately does *not* fall back to bundled copy here, so a broken key cannot look like a working install.
- **Fallback:** Bundled English copy shown if the API is unreachable.
- **Dismissing:** the confirm and dismiss buttons, a tap outside the card, and the Android back button all close the dialog. The dialog's canvas is disabled until the card is on screen, so a slow header image never blocks input.
- **EventSystem:** the SDK creates one if your scene has none, so its buttons always respond. Ship your own to keep input handling under your control.
- **Editor:** every entry point is a no-op in Play Mode - no dialog, no native sheet, no billed events, and no `PlayerPrefs` written.
- **IL2CPP:** the SDK ships `Runtime/link.xml` and `[Preserve]` attributes, so managed stripping at Medium or High cannot remove the JSON model fields.
- **The two buttons differ:** confirm fires `SKStoreReviewController` / `ReviewManager`, dismiss does not. A dismissal records the cooldown and sends `pre_prompt_dismissed`. This is deliberate: a player who says "not now" is answering the question, so the rating sheet stays closed.
- **iOS native API:** the Unity SDK stays on `SKStoreReviewController.requestReview(in:)` on every iOS version, unlike the native iOS SDK, which uses `AppStore.requestReview(in:)` from iOS 16. The newer API is Swift-only and the Unity shim (`Runtime/Plugins/iOS/PromptSurgeNative.mm`) is Objective-C++; switching would add Swift to every game's Xcode export. Both APIs share the same 3-in-365-days limit.

## The copy is where the policy risk lives

The bundled default is a statement, not a question, and it is the same on all three SDKs and on
the server's own fallback:

| Field | Default |
|---|---|
| Title | A quick favor |
| Body | A short review helps other people discover apps like this. |
| Confirm | Sure |
| Dismiss | Not now |

It asks nothing, because Google's in-app review guidance says an app should not ask the user any
questions before or while presenting the rating button or card, and the buttons already carry the
choice. Copy you set in the dashboard replaces it.

Because only the confirm button opens the native sheet, rewriting the four strings into a
satisfaction question - "Are you enjoying the game?", "How are we doing?" - turns the dialog into
a filter that routes only happy players to the store. That is the pattern Apple guideline 5.6.1
and Google Play's in-app review policy are about, and the consequence lands on your listing.

Keep it a request to review. Do not make it a question about how the player feels.

## App Store review notes

App Review sees the pre-prompt in front of the rating sheet, so say what it does before they ask.
Paste this into App Store Connect, under your game's version, **App Review Information > Notes**,
with `[action]` replaced by the moment you call `RequestReview()` (and your own button labels if
you changed them):

```
After a user completes [action], the app shows a short request to leave a review. Tapping Sure opens the system rating sheet; tapping Not now closes it. The app never asks how the user feels and never collects a rating itself.
```

Play has no reviewer-notes field for the in-app review flow.

## QA

`PromptSurge.SetRateLimitBypass(true)` disables the 90/7-day cooldowns and the store review quota check on that device so a tester can trigger the dialog repeatedly. It is persisted in `PlayerPrefs` and logs a warning on every check while it is on. Never call it from a build you ship: with the quota check off, a "Sure" can open nothing.

## Requirements notes

Input handling set to **Input System (New) only**: the dialog works, but the Android back button does not dismiss it - the SDK does not read the new Input System per frame. Tapping outside the card still dismisses. Both **Input Manager (Old)** and **Both** are unaffected.

## Privacy

### What the SDK collects

One identifier per device: Unity's own `SystemInfo.deviceUniqueIdentifier`, sent as Unity provides
it. On Android that is an MD5 hash of the `ANDROID_ID`. On iOS it is Apple's `identifierForVendor`
(IDFV), **not hashed** - Unity's iOS trampoline returns `identifierForVendor.UUIDString`. The IDFV
is not the advertising identifier: Apple scopes it to one vendor's apps on one device and resets it
when all of that vendor's apps are removed. The SDK does not write the identifier to its logs, and
no advertising identifier is used anywhere.

This differs from the native Android and iOS SDKs, which send
`SHA-256(device identifier + package or bundle ID)`. Doing the same here would give every existing
device a new identifier, so it waits for a Unity release that ships for other reasons as well. The
PromptSurge Privacy Policy describes both.

Alongside it, each event records which prompt step happened (`pre_prompt_shown`,
`pre_prompt_confirmed`, `pre_prompt_dismissed`, `native_prompt_requested`, `initialize`,
`first_open`), your app version, the SDK version and the device locale. Nothing else.

**There is no IDFA and no ATT prompt.** You do not need to add one because of this SDK.

### iOS builds: you do not need a privacy manifest for this SDK

**This SDK ships no `PrivacyInfo.xcprivacy` and does not need one.** It has no framework of its own -
its C# compiles into **UnityFramework**, and Unity already writes that target's manifest for you.

The only required-reason API the SDK touches is `UserDefaults`, and it only ever reaches it through
`PlayerPrefs`. Unity's own API, so Unity's own declaration: the generated
`UnityFramework/PrivacyInfo.xcprivacy` carries `NSPrivacyAccessedAPICategoryUserDefaults` with
reason **CA92.1**, annotated *"Used for PlayerPrefs API"*. The native shim
(`Runtime/Plugins/iOS/PromptSurgeNative.mm`) uses only StoreKit and UIKit, which are not
required-reason APIs. There is nothing left for us to declare.

**One version floor to know about.** Unity emits that declaration from **2022.3.18f1** onward (and
2021.3.35f1 / 2023.2.7f1 on the older streams). On 2022.3.0-2022.3.17 it is absent, and a build can
hit **ITMS-91053** at App Store submission from a project that compiled and ran perfectly. Update
the Editor to a current 2022.3 patch, or add the CA92.1 entry to your own manifest by hand.

Your app's own manifest, if you have one, is unaffected either way - Apple aggregates one per
binary, and your app and UnityFramework are different binaries.

### What to declare in each store

**App Store Connect &rsaquo; App Privacy**

| Data type | Purpose | Linked to identity | Used for tracking |
| --- | --- | --- | --- |
| Identifiers &rsaquo; Device ID | Analytics | **No** | **No** |
| Usage Data &rsaquo; Product Interaction | Analytics | **No** | **No** |

**Google Play &rsaquo; Data Safety**

| Data type | Collected | Shared | Purpose | Linked to user |
| --- | --- | --- | --- | --- |
| Device or other IDs | Yes | No | Analytics | **No** |
| App activity &rsaquo; App interactions | Yes | No | Analytics | **No** |

Shared is No in both: the data goes to PromptSurge as your processor to run the feature you
integrated, not on to a third party. Encrypted in transit: yes, every request is HTTPS. Declare
these in addition to whatever the rest of your game collects.
