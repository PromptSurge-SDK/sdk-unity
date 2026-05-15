using System;
using UnityEngine;

namespace PromptSurgeSDK.Internal {
    // ── Internal UI model (used throughout the SDK) ──────────────────────────

    [Serializable]
    internal class PromptText {
        public string title;
        public string body;
        public string positiveButton;
        public string negativeButton;
        public string locale;
    }

    [Serializable]
    internal class DialogTheme {
        public string backgroundColor;
        public string textColor;
        public string positiveButtonColor;
        public string negativeButtonColor;
    }

    [Serializable]
    internal class PromptResponse {
        public string promptId;
        public int appPromptNumber;
        public PromptText text;
        public DialogTheme theme;
        public string imageUrl;
    }

    // ── API wire model (matches actual JSON from /v1/prompts) ────────────────
    // Fields are flat — no nested "text" or "theme" objects.

    [Serializable]
    internal class APIDialogTheme {
        public string backgroundColor;
        public string textColor;
        public string accentColor;      // → positiveButtonColor
        public string buttonTextColor;  // → negativeButtonColor
    }

    [Serializable]
    internal class APIPromptResponse {
        public string promptId;
        public int    promptNumber;     // → appPromptNumber
        public string title;
        public string body;
        public string ctaConfirm;       // → positiveButton
        public string ctaDismiss;       // → negativeButton
        public string locale;
        public string imageUrl;
        public APIDialogTheme theme;
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    internal static class ApiMapper {
        internal static PromptResponse Map(APIPromptResponse api) {
            if (api == null) return null;

            var text = new PromptText {
                title          = api.title,
                body           = api.body,
                positiveButton = api.ctaConfirm,
                negativeButton = api.ctaDismiss,
                locale         = api.locale,
            };

            DialogTheme theme = null;
            if (api.theme != null) {
                theme = new DialogTheme {
                    backgroundColor    = api.theme.backgroundColor,
                    textColor          = api.theme.textColor,
                    positiveButtonColor = api.theme.accentColor,
                    negativeButtonColor = api.theme.buttonTextColor,
                };
            }

            return new PromptResponse {
                promptId        = api.promptId,
                appPromptNumber = api.promptNumber,
                text            = text,
                theme           = theme,
                imageUrl        = api.imageUrl,
            };
        }
    }

    // ── Defaults ─────────────────────────────────────────────────────────────

    internal static class Defaults {
        internal static readonly PromptText Text = new PromptText {
            title          = "Enjoying the app?",
            body           = "We’d love to hear your feedback! Would you like to leave a quick review?",
            positiveButton = "Sure!",
            negativeButton = "Not now",
            locale         = "en",
        };

        internal static readonly PromptResponse Response = new PromptResponse {
            promptId = "default",
            text     = Text,
            theme    = null,
        };
    }
}
