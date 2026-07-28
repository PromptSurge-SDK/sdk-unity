using System;
using UnityEngine.Scripting;

namespace PromptSurgeSDK.Internal {
    // Every model here is populated by JsonUtility via reflection, so IL2CPP managed stripping
    // at Medium or High would happily remove the fields. [Preserve] plus Runtime/link.xml keeps
    // them: without both, the dialog silently falls back to bundled English on a customer's
    // device build and works perfectly in the editor.

    // ── Internal UI model (used throughout the SDK) ──────────────────────────

    [Serializable, Preserve]
    internal class PromptText {
        public string title;
        public string body;
        public string positiveButton;
        public string negativeButton;
        public string locale;
    }

    /// <summary>
    /// Resolved theme. Field names deliberately describe the ROLE of each colour, because the
    /// old names ("negativeButtonColor" for what the server calls buttonTextColor) are what led
    /// to a foreground colour being painted onto a button background.
    /// </summary>
    [Serializable, Preserve]
    internal class DialogTheme {
        public string presetId;
        /// Fill of the confirm button, and the label colour of the dismiss button.
        public string accentColor;
        /// Card background.
        public string backgroundColor;
        /// Title and body text.
        public string textColor;
        /// Label colour on top of <see cref="accentColor"/>. Never a background.
        public string buttonTextColor;
    }

    [Serializable, Preserve]
    internal class PromptResponse {
        public string promptId;
        public int appPromptNumber;
        public PromptText text;
        public DialogTheme theme;
        public string imageUrl;
        /// <summary>
        /// True during the mandatory warm-up phase. SDK fires native review without dialog.
        /// Never cached — always reflects the live server state.
        /// </summary>
        public bool warmup;
    }

    // ── API wire model (matches actual JSON from /v1/prompts) ────────────────
    // Fields are flat — no nested "text" object. The theme object is verbatim what
    // resolveTheme() in apps/api/src/routes/adminAppearance.ts sends.

    [Serializable, Preserve]
    internal class APIDialogTheme {
        public string presetId;
        public string accentColor;
        public string backgroundColor;
        public string textColor;
        public string buttonTextColor;
    }

    [Serializable, Preserve]
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
        /// <summary>
        /// True during the mandatory warm-up phase (first 50 distinct-device events).
        /// The SDK fires native review without showing the pre-prompt dialog.
        /// </summary>
        public bool warmup;
    }

    /// <summary>Error envelope, e.g. <c>{"error":"app_deleted"}</c>.</summary>
    [Serializable, Preserve]
    internal class APIErrorResponse {
        public string error;
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
                    presetId        = api.theme.presetId,
                    accentColor     = api.theme.accentColor,
                    backgroundColor = api.theme.backgroundColor,
                    textColor       = api.theme.textColor,
                    buttonTextColor = api.theme.buttonTextColor,
                };
            }

            return new PromptResponse {
                promptId        = api.promptId,
                appPromptNumber = api.promptNumber,
                text            = text,
                theme           = theme,
                imageUrl        = api.imageUrl,
                warmup          = api.warmup,
            };
        }

        /// <summary>
        /// True when the decoded object actually looks like a prompt. JsonUtility never throws on
        /// a shape mismatch — it returns an object with every field left at its default — so
        /// <c>{"error":"invalid_api_key"}</c> used to sail through as a valid response.
        /// </summary>
        internal static bool LooksLikePrompt(APIPromptResponse api) =>
            api != null &&
            !string.IsNullOrEmpty(api.title) &&
            !string.IsNullOrEmpty(api.body) &&
            !string.IsNullOrEmpty(api.ctaConfirm) &&
            !string.IsNullOrEmpty(api.ctaDismiss);
    }

    // ── Defaults ─────────────────────────────────────────────────────────────

    internal static class Defaults {
        // Kept verbatim in step with the Android and iOS SDKs so the same game shows the same
        // offline copy on every platform.
        //
        // The title is a call to action, not a satisfaction question, and must stay one: only the
        // confirm button opens the native sheet, so an "Are you enjoying...?" title would make
        // this a sentiment filter (Apple 5.6.1, Google Play policy). See docs/conventions.md.
        internal static readonly PromptText Text = new PromptText {
            title          = "Leave a review?",
            body           = "Reviews help other people discover apps like this. Got a moment?",
            positiveButton = "Sure",
            negativeButton = "Not now",
            locale         = "en",
        };

        internal static readonly PromptResponse Response = new PromptResponse {
            promptId = null,
            text     = Text,
            theme    = null,
        };
    }
}
