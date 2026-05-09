using System;
using UnityEngine;
using UnityEngine.UI;

namespace PromptSurgeSDK.Internal {
    internal static class PrePromptCanvas {
        internal static void Show(PromptResponse response, Action onAccept, Action onDismiss) {
            var res   = response ?? Defaults.Response;
            var root  = BuildDialog(res, onAccept, onDismiss);
            UnityEngine.Object.DontDestroyOnLoad(root);
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
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.075f, 0.5f);
            cardRect.anchorMax = new Vector2(0.925f, 0.5f);
            cardRect.pivot     = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(0, 0);

            // Block touches on the card from reaching the dim
            var cardBtn = card.AddComponent<Button>();
            cardBtn.transition = Selectable.Transition.None;

            var vl = card.AddComponent<VerticalLayoutGroup>();
            vl.padding             = new RectOffset(40, 40, 40, 40);
            vl.spacing             = 20;
            vl.childAlignment      = TextAnchor.MiddleCenter;
            vl.childControlWidth   = true;
            vl.childForceExpandWidth = true;
            vl.childControlHeight  = false;
            vl.childForceExpandHeight = false;
            var csf = card.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textColor = ParseHex(res.theme?.textColor) ?? new Color(0.10f, 0.12f, 0.18f);
            AddText(card, res.text?.title ?? Defaults.Text.title, 42, FontStyle.Bold, textColor);
            AddText(card, res.text?.body  ?? Defaults.Text.body,  32, FontStyle.Normal, textColor);

            // Button row
            var row = new GameObject("Buttons");
            row.transform.SetParent(card.transform, false);
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing              = 20;
            hl.childForceExpandWidth = true;
            hl.childControlHeight   = true;
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 88;

            var posColor = ParseHex(res.theme?.positiveButtonColor) ?? new Color(0.07f, 0.62f, 0.50f);
            var negColor = ParseHex(res.theme?.negativeButtonColor) ?? new Color(0.55f, 0.55f, 0.60f);

            CreateButton(row, res.text?.positiveButton ?? Defaults.Text.positiveButton,
                         posColor, Color.white,
                         () => { UnityEngine.Object.Destroy(root); onAccept?.Invoke(); });

            CreateButton(row, res.text?.negativeButton ?? Defaults.Text.negativeButton,
                         new Color(0.94f, 0.94f, 0.96f), negColor,
                         () => { UnityEngine.Object.Destroy(root); onDismiss?.Invoke(); });

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
    }
}
