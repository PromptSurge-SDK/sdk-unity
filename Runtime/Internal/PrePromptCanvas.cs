using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

namespace PromptSurgeSDK.Internal {
    internal static class PrePromptCanvas {
        internal static void Show(PromptResponse response, Action onAccept, Action onDismiss) {
            var res   = response ?? Defaults.Response;
            Logger.Info($"Showing pre-prompt dialog — id={res.promptId} title=\"{res.text?.title}\"");
            var root  = BuildDialog(res, onAccept, onDismiss);
            UnityEngine.Object.DontDestroyOnLoad(root);

            // Optionally load header image asynchronously
            if (!string.IsNullOrEmpty(res.imageUrl)) {
                PromptSurgeRunner.Instance.StartCoroutine(
                    LoadHeaderImage(root, res.imageUrl));
            }
        }

        // ── Build ──────────────────────────────────────────────────────────────

        private static GameObject BuildDialog(PromptResponse res, Action onAccept, Action onDismiss) {
            var root = new GameObject("[PromptSurge Dialog]");

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            root.AddComponent<GraphicRaycaster>();

            // Full-screen dim — tap to dismiss
            var dim = CreatePanel(root, new Color(0, 0, 0, 0.5f), stretch: true);
            var dimBtn = dim.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(() => { UnityEngine.Object.Destroy(root); onDismiss?.Invoke(); });

            // Card
            var cardColor = ParseHex(res.theme?.backgroundColor) ?? Color.white;
            var card = CreatePanel(root, cardColor, stretch: false);
            card.name = "ps_card";
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.075f, 0.5f);
            cardRect.anchorMax = new Vector2(0.925f, 0.5f);
            cardRect.pivot     = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(0, 0);

            // Block touches on the card from reaching the dim
            var cardBtn = card.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.None;

            var vl = card.AddComponent<VerticalLayoutGroup>();
            vl.padding             = new RectOffset(0, 0, 0, 40);
            vl.spacing             = 0;
            vl.childAlignment      = TextAnchor.MiddleCenter;
            vl.childControlWidth   = true;
            vl.childForceExpandWidth = true;
            vl.childControlHeight  = false;
            vl.childForceExpandHeight = false;
            var csf = card.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header image placeholder (zero height; filled once image loads)
            var imgGo = new GameObject("HeaderImage");
            imgGo.transform.SetParent(card.transform, false);
            imgGo.AddComponent<RawImage>().color = Color.clear;
            var imgLe = imgGo.AddComponent<LayoutElement>();
            imgLe.preferredHeight = 0;
            imgLe.flexibleWidth   = 1;
            imgGo.name = "ps_header_image"; // used to find it when setting the texture

            // Text section with its own padding
            var textSection = new GameObject("TextSection");
            textSection.transform.SetParent(card.transform, false);
            var textVl = textSection.AddComponent<VerticalLayoutGroup>();
            textVl.padding            = new RectOffset(40, 40, 40, 0);
            textVl.spacing            = 20;
            textVl.childAlignment     = TextAnchor.MiddleCenter;
            textVl.childControlWidth  = true;
            textVl.childForceExpandWidth = true;
            textVl.childControlHeight = false;
            textVl.childForceExpandHeight = false;
            var textCsf = textSection.AddComponent<ContentSizeFitter>();
            textCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var textLe = textSection.AddComponent<LayoutElement>();
            textLe.flexibleWidth = 1;

            var textColor = ParseHex(res.theme?.textColor) ?? new Color(0.10f, 0.12f, 0.18f);
            AddText(textSection, res.text?.title ?? Defaults.Text.title, 42, FontStyle.Bold, textColor);
            AddText(textSection, res.text?.body  ?? Defaults.Text.body,  32, FontStyle.Normal, textColor);

            // Button row (parented to textSection so it shares the same padding)
            var row = new GameObject("Buttons");
            row.transform.SetParent(textSection.transform, false);
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing              = 20;
            hl.childForceExpandWidth = true;
            hl.childControlHeight   = true;
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 88;

            var posColor = ParseHex(res.theme?.positiveButtonColor) ?? new Color(0.07f, 0.62f, 0.50f);
            var negColor = ParseHex(res.theme?.negativeButtonColor) ?? new Color(0.55f, 0.55f, 0.60f);

            // Negative (dismiss) on the left, positive (accept) on the right.
            CreateButton(row, res.text?.negativeButton ?? Defaults.Text.negativeButton,
                         new Color(0.94f, 0.94f, 0.96f), negColor,
                         () => {
                             Logger.Info("Pre-prompt dismissed via 'Not now' button.");
                             UnityEngine.Object.Destroy(root);
                             onDismiss?.Invoke();
                         });

            CreateButton(row, res.text?.positiveButton ?? Defaults.Text.positiveButton,
                         posColor, Color.white,
                         () => {
                             Logger.Info("Pre-prompt confirmed via 'Sure!' button.");
                             UnityEngine.Object.Destroy(root);
                             onAccept?.Invoke();
                         });

            return root;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static GameObject CreatePanel(GameObject parent, Color color, bool stretch) {
            var go   = new GameObject("Panel");
            go.transform.SetParent(parent.transform, false);
            var img  = go.AddComponent<Image>();
            img.color = color;
            var rect = go.GetComponent<RectTransform>();
            if (stretch) {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
            }
            return go;
        }

        private static void AddText(GameObject parent, string content, int fontSize,
                                    FontStyle style, Color color) {
            var go   = new GameObject("Text");
            go.transform.SetParent(parent.transform, false);
            var txt  = go.AddComponent<Text>();
            txt.text      = content;
            txt.fontSize  = fontSize;
            txt.fontStyle = style;
            txt.color     = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow   = VerticalWrapMode.Overflow;
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            // Allow text to declare its own preferred height
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void CreateButton(GameObject parent, string label,
                                         Color bgColor, Color textColor, Action onClick) {
            var go  = new GameObject("Button");
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;

            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.text      = label;
            txt.fontSize  = 32;
            txt.fontStyle = FontStyle.Bold;
            txt.color     = textColor;
            txt.alignment = TextAnchor.MiddleCenter;
            var r = txtGo.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
        }

        private static Color? ParseHex(string hex) {
            if (string.IsNullOrEmpty(hex)) return null;
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return null;
            try {
                return new Color(
                    Convert.ToInt32(hex.Substring(0, 2), 16) / 255f,
                    Convert.ToInt32(hex.Substring(2, 2), 16) / 255f,
                    Convert.ToInt32(hex.Substring(4, 2), 16) / 255f);
            } catch {
                return null;
            }
        }

        /// Loads a header image from URL and inserts it at the top of the dialog card.
        private static IEnumerator LoadHeaderImage(GameObject root, string url) {
            using var req = UnityWebRequestTexture.GetTexture(url);
            yield return req.SendWebRequest();

            if (root == null) yield break; // dialog was dismissed
            if (req.result != UnityWebRequest.Result.Success) yield break;

            var imgGo = root.transform.Find("ps_card/ps_header_image")?.gameObject;
            if (imgGo == null) yield break;

            var texture = DownloadHandlerTexture.GetContent(req);
            var rawImg  = imgGo.GetComponent<RawImage>();
            if (rawImg == null || texture == null) yield break;

            rawImg.texture = texture;
            rawImg.color   = Color.white;

            // Compute height from aspect ratio (max 320 px in reference resolution)
            float aspect = (float)texture.height / texture.width;
            float refWidth = 1080f * 0.85f; // card width fraction
            float height = Mathf.Min(refWidth * aspect, 320f);

            var le = imgGo.GetComponent<LayoutElement>();
            if (le != null) le.preferredHeight = height;
        }
    }
}
