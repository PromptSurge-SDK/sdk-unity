using System;
using UnityEngine;

namespace PromptSurgeSDK.Internal {
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
