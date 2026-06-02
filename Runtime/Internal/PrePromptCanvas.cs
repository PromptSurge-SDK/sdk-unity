using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using PromptSurgeSDK.Packages.PromptSurge.Runtime.Internal;

namespace PromptSurgeSDK.Internal
{
    internal static class PrePromptCanvas
    {
        private const string PrefabResourcePath = "PromptSurge/PromptSurgeDialog";

        internal static void Show(PromptResponse response, Action onAccept, Action onDismiss)
        {
            var res = response ?? Defaults.Response;
            Logger.Info($"Showing pre-prompt dialog — id={res.promptId} title=\"{res.text?.title}\"");
            PromptSurgeRunner.Instance.StartCoroutine(ShowRoutine(res, onAccept, onDismiss));
        }

        private static IEnumerator ShowRoutine(PromptResponse res, Action onAccept, Action onDismiss)
        {
            var request = Resources.LoadAsync<GameObject>(PrefabResourcePath);
            yield return request;

            var prefab = request.asset as GameObject;
            if (prefab == null)
            {
                Logger.Error($"Pre-prompt dialog prefab not found at Resources/{PrefabResourcePath}.");
                yield break;
            }

            var root = UnityEngine.Object.Instantiate(prefab);
            root.name = "[PromptSurge Dialog]";
            UnityEngine.Object.DontDestroyOnLoad(root);

            var layout = root.GetComponent<DialogLayout>();
            if (layout == null)
            {
                Logger.Error("Pre-prompt dialog prefab is missing a DialogLayout component.");
                UnityEngine.Object.Destroy(root);
                yield break;
            }

            Populate(root, layout, res, onAccept, onDismiss);

            // Optionally load the header image asynchronously
            if (!string.IsNullOrEmpty(res.imageUrl))
            {
                yield return LoadHeaderImage(root, layout, res.imageUrl);
            }
        }

        private static void Populate(GameObject root, DialogLayout layout, PromptResponse res,
            Action onAccept, Action onDismiss)
        {
            var cardColor = ParseHex(res.theme?.backgroundColor) ?? Color.white;
            var textColor = ParseHex(res.theme?.textColor) ?? new Color(0.10f, 0.12f, 0.18f);
            var posColor = ParseHex(res.theme?.positiveButtonColor) ?? new Color(0.07f, 0.62f, 0.50f);
            var negColor = ParseHex(res.theme?.negativeButtonColor) ?? new Color(0.55f, 0.55f, 0.60f);

            if (layout.background != null) layout.background.color = cardColor;

            if (layout.header != null)
            {
                layout.header.text = res.text?.title ?? Defaults.Text.title;
                layout.header.color = textColor;
            }

            if (layout.message != null)
            {
                layout.message.text = res.text?.body ?? Defaults.Text.body;
                layout.message.color = textColor;
            }

            // Negative (dismiss) button on the left.
            ConfigureButton(layout.button1,
                res.text?.negativeButton ?? Defaults.Text.negativeButton,
                negColor,
                posColor, // Text color is like positive button.
                () =>
                {
                    Logger.Info("Pre-prompt dismissed via 'Not now' button.");
                    UnityEngine.Object.Destroy(root);
                    onDismiss?.Invoke();
                });

            // Positive (accept) button on the right.
            ConfigureButton(layout.button2,
                res.text?.positiveButton ?? Defaults.Text.positiveButton,
                posColor,
                cardColor, // Text color is like card color.
                () =>
                {
                    Logger.Info("Pre-prompt confirmed via 'Sure!' button.");
                    UnityEngine.Object.Destroy(root);
                    onAccept?.Invoke();
                });
        }

        private static void ConfigureButton(ButtonLayout button, string label,
            Color bgColor, Color textColor, Action onClick)
        {
            if (button == null) return;
            if (button.text != null)
            {
                button.text.text = label;
                button.text.color = textColor;
            }

            if (button.background != null) button.background.color = bgColor;
            if (button.button != null) button.button.onClick.AddListener(() => onClick?.Invoke());
        }

        private static Color? ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return null;
            try
            {
                return new Color(
                    Convert.ToInt32(hex.Substring(0, 2), 16) / 255f,
                    Convert.ToInt32(hex.Substring(2, 2), 16) / 255f,
                    Convert.ToInt32(hex.Substring(4, 2), 16) / 255f);
            }
            catch
            {
                return null;
            }
        }

        /// Loads a header image from URL and reveals it at the top of the dialog card.
        private static IEnumerator LoadHeaderImage(GameObject root, DialogLayout layout, string url)
        {
            using var req = UnityWebRequestTexture.GetTexture(url);
            yield return req.SendWebRequest();

            if (root == null) yield break; // dialog was dismissed
            if (req.result != UnityWebRequest.Result.Success) yield break;

            var rawImg = layout != null ? layout.image : null;
            if (rawImg == null) yield break;

            var texture = DownloadHandlerTexture.GetContent(req);
            if (texture == null) yield break;

            rawImg.texture = texture;
            rawImg.color = Color.white;
            rawImg.gameObject.SetActive(true);

            // Compute height from aspect ratio (max 320 px in reference resolution)
            float aspect = (float)texture.height / texture.width;
            float refWidth = 1080f * 0.85f; // card width fraction
            float height = Mathf.Min(refWidth * aspect, 320f);

            var le = rawImg.GetComponent<LayoutElement>();
            if (le != null) le.preferredHeight = height;
        }
    }
}