using System;
using UnityEngine;
using UnityEngine.UI;
using PromptSurgeSDK.Packages.PromptSurge.Runtime.Internal;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// Owns a <see cref="DialogLayout"/>, populates it from a <see cref="PromptResponse"/>, and
    /// exposes events for the buttons. Pure view — no async, coroutine, or web code lives here.
    /// </summary>
    internal class DialogView {
        private readonly DialogLayout _layout;

        public event Action Confirmed;
        public event Action Dismissed;

        /// <summary>
        /// True once the header image has either landed or definitively failed. The presenter
        /// waits on this, briefly, before revealing the card — it must never wait on the request
        /// itself, which has no bounded duration the presenter can rely on.
        /// </summary>
        public bool HeaderImageResolved { get; private set; }

        /// <summary>Marks the image as resolved without setting one (it failed, or was empty).</summary>
        public void MarkHeaderImageResolved() => HeaderImageResolved = true;

        public DialogView(DialogLayout layout, PromptResponse res) {
            _layout = layout;
            Bind(res);
        }

        // ── Bind ───────────────────────────────────────────────────────────────

        private void Bind(PromptResponse res) {
            // Matches the Android dialog: confirm is filled with the accent colour and labelled in
            // buttonTextColor; dismiss is flat against the card and labelled in the accent colour.
            //
            // This used to paint the dismiss button's BACKGROUND with buttonTextColor, which is a
            // foreground colour. On `system` and `greyscale` that is white-on-white; on the dark
            // presets it is a near-black slab. The button was invisible on every shipped preset.
            var cardColor   = ParseHex(res.theme?.backgroundColor) ?? Color.white;
            var textColor   = ParseHex(res.theme?.textColor)       ?? new Color(0.10f, 0.12f, 0.18f);
            var accentColor = ParseHex(res.theme?.accentColor)     ?? new Color(0.07f, 0.62f, 0.50f);
            var buttonText  = ParseHex(res.theme?.buttonTextColor) ?? Color.white;

            if (_layout.background != null) _layout.background.color = cardColor;

            if (_layout.header != null) {
                _layout.header.text  = res.text?.title ?? Defaults.Text.title;
                _layout.header.color = textColor;
            }

            if (_layout.message != null) {
                _layout.message.text  = res.text?.body ?? Defaults.Text.body;
                _layout.message.color = textColor;
            }

            // Negative (dismiss) button on the left: flat against the card, accent label.
            ConfigureButton(_layout.button1,
                res.text?.negativeButton ?? Defaults.Text.negativeButton,
                cardColor,
                accentColor,
                () => Dismissed?.Invoke());

            // Positive (accept) button on the right: accent fill, buttonTextColor label.
            ConfigureButton(_layout.button2,
                res.text?.positiveButton ?? Defaults.Text.positiveButton,
                accentColor,
                buttonText,
                () => Confirmed?.Invoke());
        }

        public void SetDialogActive(bool active)
        {
            if (_layout.card != null)
                _layout.card.gameObject.SetActive(active);
        }

        /// Reveals the header image at the top of the dialog card once its texture is available.
        public void SetHeaderImage(Texture2D texture) {
            HeaderImageResolved = true;
            var rawImg = _layout != null ? _layout.image : null;
            if (rawImg == null || texture == null) return;

            rawImg.texture = texture;
            rawImg.color   = Color.white;
            rawImg.gameObject.SetActive(true);

            // Compute height from aspect ratio (max 320 px in reference resolution)
            float aspect   = (float)texture.height / texture.width;
            float refWidth = 1080f * 0.85f; // card width fraction
            float height   = Mathf.Min(refWidth * aspect, 320f);

            var le = rawImg.GetComponent<LayoutElement>();
            if (le != null) le.preferredHeight = height;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static void ConfigureButton(ButtonLayout button, string label,
            Color bgColor, Color textColor, Action onClick) {
            if (button == null) return;
            if (button.text != null) {
                button.text.text  = label;
                button.text.color = textColor;
            }

            if (button.background != null) button.background.color = bgColor;
            if (button.button != null) button.button.onClick.AddListener(() => onClick?.Invoke());
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
