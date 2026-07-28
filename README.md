# PromptSurge Unity SDK

Unified Android + iOS review prompt SDK for Unity 2021.3+. Shows a pre-prompt dialog before triggering the native OS review sheet, increasing tap-through rates.

## Requirements

- Unity 2021.3 LTS or later
- Android: Play Store distribution, `minSdkVersion 21`
- iOS: iOS 14+ deployment target

## Installation

### Option A — Unity Package Manager (Git URL)

In Unity: **Window → Package Manager → + → Add package from git URL**

```
https://github.com/PromptSurge-SDK/sdk-unity.git
```

Pin a version by appending `#v1.1.0`. Without a tag the Package Manager tracks the default branch, which moves.

### Option B — Local tarball

Download `promptsurge-unity-1.1.0.tgz` and use **Add package from tarball**.

## Android dependency

The SDK uses Google Play's In-App Review API. Add the dependency to your project one of two ways:

**With [External Dependency Manager (EDM4U)](https://github.com/googlesamples/unity-jar-resolver):**  
The included `Editor/PromptSurgeDependencies.xml` is picked up automatically. No manual step needed.

**Without EDM4U** — add to `Assets/Plugins/Android/mainTemplate.gradle`:
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

## Diagnostics

Errors and warnings always print, whatever the log level: a rejected API key, a failed fetch, a missing prefab and a suppressed dialog each say so by name. `SetLogLevel(LogLevel.Info)` adds a line per decision, `LogLevel.Verbose` adds per-event delivery results. Filter the console for `[PromptSurge]`.

If nothing appears at all in a device build, `Initialize` was never called.

## Behaviour

- **Holdout group:** 10% of devices silently skipped (stored in `PlayerPrefs`).
- **Rate limiting:** 90-day cooldown after shown; 7-day after dismiss. The cooldown is recorded when the dialog actually appears, not when it is requested.
- **Impression limit:** When your plan's monthly cap is reached the API returns `402`. The SDK persists this in `PlayerPrefs`, suppresses the dialog and fires the native OS review sheet directly. The flag clears on the next successful response, i.e. when the billing period rolls over.
- **Deleted apps:** if the app is deleted in the admin panel the API returns `404 app_deleted` and the pre-prompt is suppressed while the native sheet still fires. Restoring the app clears the flag.
- **Invalid API key:** `401`/`403` is logged as an error and no pre-prompt is shown. The SDK deliberately does *not* fall back to bundled copy here, so a broken key cannot look like a working install.
- **Fallback:** Bundled English copy shown if the API is unreachable.
- **Dismissing:** the confirm and dismiss buttons, a tap outside the card, and the Android back button all close the dialog. The dialog's canvas is disabled until the card is on screen, so a slow header image never blocks input.
- **EventSystem:** the SDK creates one if your scene has none, so its buttons always respond. Ship your own to keep input handling under your control.
- **Editor:** every entry point is a no-op in Play Mode — no dialog, no native sheet, no billed events, and no `PlayerPrefs` written.
- **IL2CPP:** the SDK ships `Runtime/link.xml` and `[Preserve]` attributes, so managed stripping at Medium or High cannot remove the JSON model fields.
- **No sentiment gating:** Both buttons fire `SKStoreReviewController` / `ReviewManager` — compliant with Apple guideline 5.6.1 and Google Play policy.

## QA

`PromptSurge.SetRateLimitBypass(true)` disables the 90/7-day cooldowns on that device so a tester can trigger the dialog repeatedly. It is persisted in `PlayerPrefs` and logs a warning on every check while it is on. Never call it from a build you ship.

## Requirements notes

Input handling set to **Input System (New) only**: the dialog works, but the Android back button does not dismiss it — the SDK does not read the new Input System per frame. Tapping outside the card still dismisses. Both **Input Manager (Old)** and **Both** are unaffected.

## Privacy

### What the SDK collects

One identifier per install, and it differs by platform because the underlying device id does:
on Android `SHA-256(ANDROID_ID + package name)`, on iOS `SystemInfo.deviceUniqueIdentifier`, which
Unity already hashes per app. No raw device identifier is stored, transmitted or logged, and no
advertising identifier is used anywhere.

Alongside it, each event records which prompt step happened (`pre_prompt_shown`,
`pre_prompt_confirmed`, `pre_prompt_dismissed`, `native_prompt_requested`, `initialize`,
`first_open`), your app version, the SDK version and the device locale. Nothing else.

**There is no IDFA and no ATT prompt.** You do not need to add one because of this SDK.

### iOS builds: the privacy manifest is injected for you

`Editor/PromptSurgePrivacyManifest.cs` runs after Unity generates the Xcode project and adds a
`PrivacyInfo.xcprivacy` to the **UnityFramework** target - the binary this SDK's code is actually
compiled into. Your own app's manifest, if you have one, is left alone; Apple aggregates one per
binary.

This is not cosmetic. `PlayerPrefs` is `UserDefaults` on iOS, which is a required-reason API, so a
Unity game shipping this SDK without the manifest gets **ITMS-91053** at App Store submission from
a build that compiled and ran perfectly. Nothing in Unity warns about it.

If you post-process the Xcode project yourself, run your step before ours or check the file is
still there: our callback order is 100.

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
