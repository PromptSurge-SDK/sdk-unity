using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using PromptSurgeSDK.Packages.PromptSurge.Runtime.Internal;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// Presenter for the pre-prompt dialog: loads the prefab asynchronously, wires a
    /// <see cref="DialogView"/> over its <see cref="DialogLayout"/>, owns the dialog lifecycle
    /// (destroy + telemetry callbacks), and orchestrates the async header-image download.
    /// </summary>
    internal static class PrePromptCanvas {
        // Loaded via Resources.LoadAsync — lives at Resources/PromptSurge/PromptSurgeDialog.prefab.
        private const string PrefabResourcePath = "PromptSurge/PromptSurgeDialog";

        internal static void Show(PromptResponse response, Action onAccept, Action onDismiss) {
            var res = response ?? Defaults.Response;
            Logger.Info($"Showing pre-prompt dialog — id={res.promptId} title=\"{res.text?.title}\"");
            PromptSurgeRunner.Instance.StartCoroutine(ShowRoutine(res, onAccept, onDismiss));
        }

        private static IEnumerator ShowRoutine(PromptResponse res, Action onAccept, Action onDismiss) {
            var request = Resources.LoadAsync<GameObject>(PrefabResourcePath);
            yield return request;

            var prefab = request.asset as GameObject;
            if (prefab == null) {
                Logger.Error($"Pre-prompt dialog prefab not found at Resources/{PrefabResourcePath}.");
                yield break;
            }

            var root = UnityEngine.Object.Instantiate(prefab);
            root.name = "[PromptSurge Dialog]";
            UnityEngine.Object.DontDestroyOnLoad(root);

            var layout = root.GetComponent<DialogLayout>();
            if (layout == null) {
                Logger.Error("Pre-prompt dialog prefab is missing a DialogLayout component.");
                UnityEngine.Object.Destroy(root);
                yield break;
            }

            var view = new DialogView(layout, res);
            view.Confirmed += () => {
                Logger.Info("Pre-prompt confirmed via 'Sure!' button.");
                UnityEngine.Object.Destroy(root);
                onAccept?.Invoke();
            };
            view.Dismissed += () => {
                Logger.Info("Pre-prompt dismissed via 'Not now' button.");
                UnityEngine.Object.Destroy(root);
                onDismiss?.Invoke();
            };

            // Hide the card until everything (incl. the header image) is ready — the dim
            // background stays visible. The presenter reveals it via Show().
            view.SetDialogActive(false);

            // Optionally load the header image asynchronously before revealing the dialog, so the
            // user never sees the image pop in at runtime.
            if (!string.IsNullOrEmpty(res.imageUrl)) {
                yield return LoadHeaderImage(root, view, res.imageUrl);
            }

            view.SetDialogActive(true);
        }

        /// Downloads a header image from URL and hands the finished texture to the view.
        private static IEnumerator LoadHeaderImage(GameObject root, DialogView view, string url) {
            using var req = UnityWebRequestTexture.GetTexture(url);
            yield return req.SendWebRequest();

            if (root == null) yield break; // dialog was dismissed
            if (req.result != UnityWebRequest.Result.Success) yield break;

            var texture = DownloadHandlerTexture.GetContent(req);
            if (texture == null) yield break;

            view.SetHeaderImage(texture);
        }
    }
}
