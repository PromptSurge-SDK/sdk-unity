namespace PromptSurgeSDK.Internal {
    internal static class EventTypes {
        // Lifecycle. Without these, Unity installs and DAU are simply absent from
        // the dashboard while Android and iOS report both, so the numbers read as
        // "nobody ships on Unity" rather than as a missing event.
        internal const string Initialize           = "initialize";
        internal const string FirstOpen            = "first_open";

        internal const string PrePromptShown       = "pre_prompt_shown";
        internal const string PrePromptConfirmed   = "pre_prompt_confirmed";
        internal const string PrePromptDismissed   = "pre_prompt_dismissed";
        internal const string NativePromptRequested = "native_prompt_requested";
    }
}
