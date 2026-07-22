using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using PromptSurgeSDK.Packages.PromptSurge.Runtime.Internal;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// Presenter for the pre-prompt dialog: loads the prefab asynchronously, wires a
    /// <see cref="DialogView"/> over its <see cref="DialogLayout"/>, owns the dialog lifecycle
    /// (destroy + telemetry callbacks), and orchestrates the async header-image download.
    ///
    /// The prefab's canvas is a full-screen, raycast-blocking overlay at sorting order 32767 on a
    /// DontDestroyOnLoad object. Nothing here may leave it visible-and-blocking in a state the
    /// player cannot get out of, so:
    ///   - the Canvas and its GraphicRaycaster stay DISABLED until the card is ready to show,
    ///     which means a slow or hanging image download blocks nothing at all;
    ///   - the image gets a request timeout and a shorter reveal deadline, and the dialog appears
    ///     without it if either expires;
    ///   - the scrim, the back button and an EventSystem check all provide a way out.
    /// </summary>
    internal static class PrePromptCanvas {
        // Loaded via Resources.LoadAsync — lives at Resources/PromptSurge/PromptSurgeDialog.prefab.
        private const string PrefabResourcePath = "PromptSurge/PromptSurgeDialog";

        /// Hard timeout on the image request itself.
        private const int ImageRequestTimeoutSeconds = 10;

        /// How long the reveal waits for the image before showing the card without it. The image
        /// still lands later if it arrives; this only bounds how long the player waits.
        private const float ImageRevealDeadlineSeconds = 1.5f;

        /// Bounds the prefab load, which is normally instant but is async and therefore fallible.
        private const float PrefabLoadTimeoutSeconds = 5f;

        /// <summary>
        /// Shows the dialog.
        /// <paramref name="onShown"/> fires when the card is actually visible — that is the moment
        /// the impression becomes real, and the only moment it should be billed.
        /// <paramref name="onFailed"/> fires when no dialog could be presented at all. It is a
        /// separate callback on purpose: routing that case to <paramref name="onDismiss"/> would
        /// record a 7-day cooldown and send a `pre_prompt_dismissed` event for a dialog that never
        /// existed, which is worse than the failure it is reporting.
        /// </summary>
        internal static void Show(PromptResponse response, Action onShown, Action onAccept,
                                  Action onDismiss, Action onFailed) {
            var res = response ?? Defaults.Response;
            Logger.Info($"Showing pre-prompt dialog — locale={res.text?.locale} title=\"{res.text?.title}\"");
            PromptSurgeRunner.Instance.StartCoroutine(ShowRoutine(res, onShown, onAccept, onDismiss, onFailed));
        }

        private static IEnumerator ShowRoutine(PromptResponse res, Action onShown, Action onAccept,
                                               Action onDismiss, Action onFailed) {
            var request = Resources.LoadAsync<GameObject>(PrefabResourcePath);
            var loadDeadline = Time.realtimeSinceStartup + PrefabLoadTimeoutSeconds;
            while (!request.isDone && Time.realtimeSinceStartup < loadDeadline) yield return null;

            var prefab = request.asset as GameObject;
            if (prefab == null) {
                Logger.Error($"Pre-prompt dialog prefab not found at Resources/{PrefabResourcePath}. " +
                             "No dialog was shown and no impression was recorded.");
                onFailed?.Invoke();
                yield break;
            }

            var root = UnityEngine.Object.Instantiate(prefab);
            root.name = "[PromptSurge Dialog]";
            UnityEngine.Object.DontDestroyOnLoad(root);

            // Nothing is drawn and nothing is blocked until the card is ready.
            var canvas = root.GetComponent<Canvas>();
            var raycaster = root.GetComponent<GraphicRaycaster>();
            if (canvas != null) canvas.enabled = false;
            if (raycaster != null) raycaster.enabled = false;

            var layout = root.GetComponent<DialogLayout>();
            if (layout == null) {
                Logger.Error("Pre-prompt dialog prefab is missing a DialogLayout component. " +
                             "No dialog was shown and no impression was recorded.");
                UnityEngine.Object.Destroy(root);
                onFailed?.Invoke();
                yield break;
            }

            // UGUI buttons do nothing without an EventSystem, and the SDK's prefab does not ship
            // one. A host with no EventSystem — or one whose EventSystem a scene load destroyed —
            // would otherwise get a dialog whose buttons are dead.
            EnsureEventSystem();

            var closed = false;
            Action<Action> close = callback => {
                if (closed) return;
                closed = true;
                if (root != null) UnityEngine.Object.Destroy(root);
                callback?.Invoke();
            };

            var view = new DialogView(layout, res);
            view.Confirmed += () => {
                Logger.Info("Pre-prompt confirmed.");
                close(onAccept);
            };
            view.Dismissed += () => {
                Logger.Info("Pre-prompt dismissed.");
                close(onDismiss);
            };

            // Tapping the scrim outside the card dismisses. The prefab's Panel already carries a
            // Button with an empty onClick; anything with a Button that is not one of the two
            // dialog buttons is treated as scrim.
            WireScrimDismiss(root, layout, () => {
                Logger.Info("Pre-prompt dismissed by tapping outside the card.");
                close(onDismiss);
            });

            var watcher = root.AddComponent<DialogDismissWatcher>();
            watcher.OnBackPressed = () => {
                Logger.Info("Pre-prompt dismissed with the back button.");
                close(onDismiss);
            };

            // Hide the card until everything (incl. the header image) is ready — the dim
            // background stays behind it. Revealed below.
            view.SetDialogActive(false);

            // Wait briefly for the header image so it does not pop in, but never longer than the
            // deadline: an image that stalls must not hold the dialog, and before this the card
            // waited on the request with no timeout at all while the scrim ate every touch.
            if (!string.IsNullOrEmpty(res.imageUrl)) {
                PromptSurgeRunner.Instance.StartCoroutine(LoadHeaderImage(root, view, res.imageUrl));
                // realtimeSinceStartup, not Time.time: games commonly pause with timeScale = 0,
                // and a deadline that never advances is the bug this is here to prevent.
                var deadline = Time.realtimeSinceStartup + ImageRevealDeadlineSeconds;
                while (!closed && !view.HeaderImageResolved && Time.realtimeSinceStartup < deadline) {
                    yield return null;
                }
                if (!closed && !view.HeaderImageResolved) {
                    Logger.Info("Header image still loading; showing the dialog without it.");
                }
            }

            // `closed` means a button already fired its callback. `root == null` without that means
            // something outside the SDK destroyed the dialog: report it, or the caller's in-flight
            // guard stays set for the rest of the session and no prompt ever shows again.
            if (closed) yield break;
            if (root == null) {
                Logger.Warn("The pre-prompt dialog was destroyed before it could be shown.");
                onFailed?.Invoke();
                yield break;
            }

            view.SetDialogActive(true);
            if (canvas != null) canvas.enabled = true;
            if (raycaster != null) raycaster.enabled = true;
            watcher.Armed = true;

            onShown?.Invoke();
        }

        /// Downloads a header image from URL and hands the finished texture to the view.
        private static IEnumerator LoadHeaderImage(GameObject root, DialogView view, string url) {
            var req = UnityWebRequestTexture.GetTexture(url);
            req.timeout = ImageRequestTimeoutSeconds;
            yield return req.SendWebRequest();

            var success = req.result == UnityWebRequest.Result.Success;
            var error = req.error;
            Texture2D texture = null;
            if (success) texture = DownloadHandlerTexture.GetContent(req);
            req.Dispose();

            if (root == null) yield break; // dialog was dismissed
            if (!success) {
                Logger.Info($"Header image not loaded, showing the dialog without it: {error}");
                view.MarkHeaderImageResolved();
                yield break;
            }
            if (texture == null) {
                view.MarkHeaderImageResolved();
                yield break;
            }

            view.SetHeaderImage(texture);
        }

        /// <summary>
        /// Creates an EventSystem if the scene has none. The object is DontDestroyOnLoad so a
        /// scene load during the dialog's life cannot leave the buttons dead.
        /// </summary>
        private static void EnsureEventSystem() {
            if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled) return;

            Logger.Warn("No active EventSystem found — creating one so the dialog's buttons respond. " +
                        "Add an EventSystem to your scene to keep input handling under your control.");
            var go = new GameObject("[PromptSurge EventSystem]");
            go.AddComponent<EventSystem>();
            AddInputModule(go);
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void AddInputModule(GameObject go) {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // The project uses the new Input System exclusively, so StandaloneInputModule would
            // throw on its first UnityEngine.Input read. Resolve the package's module by name
            // rather than by reference, so this assembly does not depend on a package that a
            // legacy-input host will not have installed.
            var moduleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null) {
                go.AddComponent(moduleType);
                return;
            }
            Logger.Error("Input handling is set to Input System only, but InputSystemUIInputModule " +
                         "could not be found. The pre-prompt's buttons will not respond — add an " +
                         "EventSystem to your scene.");
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        /// <summary>
        /// Wires every Button in the hierarchy that is not one of the two dialog buttons to
        /// <paramref name="onDismiss"/>. In the shipped prefab that is exactly the scrim Panel,
        /// which already has a Button component with an empty onClick list.
        /// </summary>
        private static void WireScrimDismiss(GameObject root, DialogLayout layout, Action onDismiss) {
            var confirm = layout.button2 != null ? layout.button2.button : null;
            var dismiss = layout.button1 != null ? layout.button1.button : null;

            foreach (var button in root.GetComponentsInChildren<Button>(true)) {
                if (button == null || button == confirm || button == dismiss) continue;
                button.onClick.AddListener(() => onDismiss?.Invoke());
            }
        }
    }
}
