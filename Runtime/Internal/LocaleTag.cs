using System.Globalization;
using UnityEngine;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// Produces a BCP-47 language tag such as <c>de-DE</c>.
    ///
    /// The SDK used to send <c>Application.systemLanguage.ToString()</c>, i.e. the literal string
    /// "German" — as an <c>Accept-Language</c> header the server never reads, and as the
    /// <c>locale</c> field on every event. So no Unity player has ever received localized copy,
    /// and every Unity event in the database carries an English enum name where a locale belongs.
    /// </summary>
    internal static class LocaleTag {
        /// <summary>
        /// Device locale as a BCP-47 tag. Prefers <c>CultureInfo.CurrentCulture</c>, which carries
        /// the region; falls back to a language-only tag derived from
        /// <c>Application.systemLanguage</c> when the runtime reports the invariant culture, as
        /// IL2CPP builds can.
        /// </summary>
        internal static string Current() {
            var name = CultureInfo.CurrentCulture?.Name;
            if (!string.IsNullOrEmpty(name) && name.Length >= 2) return name;
            return FromSystemLanguage(Application.systemLanguage);
        }

        private static string FromSystemLanguage(SystemLanguage language) {
            switch (language) {
                case SystemLanguage.Afrikaans:  return "af";
                case SystemLanguage.Arabic:     return "ar";
                case SystemLanguage.Basque:     return "eu";
                case SystemLanguage.Belarusian: return "be";
                case SystemLanguage.Bulgarian:  return "bg";
                case SystemLanguage.Catalan:    return "ca";
                case SystemLanguage.Chinese:            return "zh";
                case SystemLanguage.ChineseSimplified:  return "zh-Hans";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                case SystemLanguage.Czech:      return "cs";
                case SystemLanguage.Danish:     return "da";
                case SystemLanguage.Dutch:      return "nl";
                case SystemLanguage.English:    return "en";
                case SystemLanguage.Estonian:   return "et";
                case SystemLanguage.Faroese:    return "fo";
                case SystemLanguage.Finnish:    return "fi";
                case SystemLanguage.French:     return "fr";
                case SystemLanguage.German:     return "de";
                case SystemLanguage.Greek:      return "el";
                case SystemLanguage.Hebrew:     return "he";
                case SystemLanguage.Hungarian:  return "hu";
                case SystemLanguage.Icelandic:  return "is";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Italian:    return "it";
                case SystemLanguage.Japanese:   return "ja";
                case SystemLanguage.Korean:     return "ko";
                case SystemLanguage.Latvian:    return "lv";
                case SystemLanguage.Lithuanian: return "lt";
                case SystemLanguage.Norwegian:  return "nb";
                case SystemLanguage.Polish:     return "pl";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Romanian:   return "ro";
                case SystemLanguage.Russian:    return "ru";
                case SystemLanguage.SerboCroatian: return "sh";
                case SystemLanguage.Slovak:     return "sk";
                case SystemLanguage.Slovenian:  return "sl";
                case SystemLanguage.Spanish:    return "es";
                case SystemLanguage.Swedish:    return "sv";
                case SystemLanguage.Thai:       return "th";
                case SystemLanguage.Turkish:    return "tr";
                case SystemLanguage.Ukrainian:  return "uk";
                case SystemLanguage.Vietnamese: return "vi";
                default:                        return "en";
            }
        }
    }
}
