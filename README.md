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
https://github.com/promptsurge/sdk-unity.git
```

### Option B — Local tarball

Download `promptsurge-unity-1.0.0.tgz` and use **Add package from tarball**.

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
}

// After a level complete, purchase, or any high-satisfaction event
void OnLevelComplete() {
    PromptSurge.RequestReview();
}
```

## Behaviour

- **Holdout group:** 10% of devices silently skipped (stored in `PlayerPrefs`).
- **Rate limiting:** 90-day cooldown after shown; 7-day after dismiss.
- **Impression limit:** When your plan's monthly cap is reached the API returns `402`. The SDK stores this flag in `PlayerPrefs` and on the next `RequestReview()` call suppresses the dialog and fires the native OS review sheet directly. Clears automatically when the next billing period begins.
- **Fallback:** Bundled English copy shown if the API is unreachable.
- **Editor:** `RequestReview()` does nothing in Play Mode — no dialog or native review sheet fires.
- **No sentiment gating:** Both buttons fire `SKStoreReviewController` / `ReviewManager` — compliant with Apple guideline 5.6.1 and Google Play policy.
