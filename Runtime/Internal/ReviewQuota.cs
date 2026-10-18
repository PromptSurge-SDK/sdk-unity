using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// One store's limit on how often it will actually show its native review UI.
    /// </summary>
    internal sealed class ReviewQuotaRule {
        internal const double DaySeconds = 86400d;

        /// <summary>Requests allowed inside <see cref="WindowSeconds"/>.</summary>
        internal readonly int Limit;
        internal readonly double WindowSeconds;
        /// <summary>Why the SDK is not asking, for the one log line a skipped call prints.</summary>
        internal readonly string Reason;

        internal ReviewQuotaRule(int limit, double windowSeconds, string reason) {
            Limit = limit;
            WindowSeconds = windowSeconds;
            Reason = reason;
        }

        /// <summary>Apple: the rating sheet shows at most three times in 365 days, per app, per device.</summary>
        internal static readonly ReviewQuotaRule AppStore = new ReviewQuotaRule(
            3, 365 * DaySeconds,
            "the SDK has already asked iOS for the rating sheet 3 times in the last 365 days, the most iOS will show");

        /// <summary>
        /// Google does not publish its quota. Its guide says calling the flow more than once within
        /// a short period (for example, less than a month) may not show a dialog.
        /// </summary>
        internal static readonly ReviewQuotaRule GooglePlay = new ReviewQuotaRule(
            1, 30 * DaySeconds,
            "the SDK already asked Google Play for the review flow in the last 30 days, and Play may show nothing when asked again that soon");
    }

    /// <summary>
    /// Mirrors the store's review quota from the SDK's own record of its native requests.
    ///
    /// The store never tells a game whether a review request displayed anything. Both stores warn
    /// against a button that leads to the review API for exactly that reason: the tap can be
    /// answered by nothing. So <see cref="ReviewRequester.Request"/> records every native request
    /// here, and once the record says the quota is used up, <c>PromptSurge.RequestReview</c> shows
    /// no pre-prompt and makes no native call. Holdout and warm-up devices are held to the same
    /// rule, so both arms of the experiment skip under the same condition.
    ///
    /// What the record cannot see: review requests the game makes itself, and requests the store
    /// declined. It is the store's published limit, not a guess at what is left.
    ///
    /// Written in C# 5 on purpose (no interpolation outside log lines, no expression bodies, no
    /// out var): scripts/unity-holdout-harness compiles this file verbatim with the C# compiler
    /// Windows ships, which knows nothing newer.
    /// </summary>
    internal static class ReviewQuota {
        /// <summary>Epoch seconds, comma separated, oldest first. Next to the RateLimiter keys.</summary>
        internal const string Key = "ps_native_review_requests";
        internal const int MaxStored = 5;

        /// <summary>The rule for the platform this build runs on. A field so the harness can pick one.</summary>
        internal static ReviewQuotaRule Rule = PlatformRule();

        /// <summary>Epoch seconds, UTC. A field so the harness can move time.</summary>
        internal static Func<double> Now = EpochNow;

        private static ReviewQuotaRule PlatformRule() {
#if UNITY_ANDROID
            return ReviewQuotaRule.GooglePlay;
#else
            return ReviewQuotaRule.AppStore;
#endif
        }

        /// <summary>Call on EVERY native review request. Keeps the last few entries only.</summary>
        internal static void RecordRequest() {
            List<double> all = Read();
            all.Add(Math.Floor(Now()));
            if (all.Count > MaxStored) all.RemoveRange(0, all.Count - MaxStored);

            var parts = new string[all.Count];
            for (int i = 0; i < all.Count; i++) {
                parts[i] = all[i].ToString("F0", CultureInfo.InvariantCulture);
            }
            PlayerPrefs.SetString(Key, string.Join(",", parts));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Epoch seconds at which the next native request becomes possible under <see cref="Rule"/>,
        /// or 0 when one is possible now.
        /// </summary>
        internal static double NextPossible() {
            return NextPossible(Read(), Now(), Rule);
        }

        /// <summary>
        /// Pure, so the rule can be tested without a clock or prefs. With k requests inside the
        /// window and a limit of n, the count drops below n when the request at position k - n
        /// (oldest first) ages out.
        /// </summary>
        internal static double NextPossible(List<double> requests, double now, ReviewQuotaRule rule) {
            var recent = new List<double>();
            foreach (double r in requests) {
                if (now - r < rule.WindowSeconds) recent.Add(r);
            }
            if (recent.Count < rule.Limit) return 0d;
            recent.Sort();
            return recent[recent.Count - rule.Limit] + rule.WindowSeconds;
        }

        /// <summary>The stored request timestamps, epoch seconds, oldest first. Unparseable entries are skipped.</summary>
        internal static List<double> Read() {
            var result = new List<double>();
            string raw = PlayerPrefs.GetString(Key, null);
            if (string.IsNullOrEmpty(raw)) return result;
            foreach (string part in raw.Split(',')) {
                double value;
                if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) {
                    result.Add(value);
                }
            }
            return result;
        }

        /// <summary>For log lines: "2027-01-05 10:00 UTC".</summary>
        internal static string FormatUtc(double epochSeconds) {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddSeconds(epochSeconds)
                .ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        }

        private static double EpochNow() {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
