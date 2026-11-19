using System;

namespace PromptSurgeSDK.Internal {
    /// <summary>
    /// Measures how long the game was away after the SDK asked the store for its rating sheet
    /// (card 833). Neither store says whether its sheet appeared, was rated or was closed; what
    /// the game CAN see is losing focus when the sheet takes over and getting it back when the
    /// sheet is gone. So after a request:
    ///   - focus lost within <see cref="ResignTimeoutSeconds"/> -> report the time until it returns;
    ///   - no focus loss by then -> the store showed nothing, report 0.
    /// Exactly one report per request.
    ///
    /// Pure C#, no UnityEngine: <see cref="PromptSurgeRunner"/> feeds it focus changes and frame
    /// ticks, and scripts/unity-holdout-harness compiles and tests it without the editor.
    /// Written in C# 5 for that harness's compiler.
    /// </summary>
    internal sealed class SheetAwayTimer {
        internal const double ResignTimeoutSeconds = 3d;

        private Action<int> _report;
        private double _startedAt;
        private double _lostAt = -1d;

        internal bool Watching { get { return _report != null; } }

        /// <summary>Starts watching one request. A watch still open is finished first with 0.</summary>
        internal void Watch(double now, Action<int> report) {
            Finish(0);
            _report = report;
            _startedAt = now;
            _lostAt = -1d;
        }

        internal void FocusChanged(bool hasFocus, double now) {
            if (_report == null) return;
            if (!hasFocus) {
                if (_lostAt < 0d) _lostAt = now;
                return;
            }
            if (_lostAt < 0d) return;
            Finish((int)Math.Max(0d, Math.Round((now - _lostAt) * 1000d)));
        }

        internal void Tick(double now) {
            if (_report == null || _lostAt >= 0d) return;
            if (now - _startedAt >= ResignTimeoutSeconds) Finish(0);
        }

        private void Finish(int awayMs) {
            var pending = _report;
            _report = null;
            _lostAt = -1d;
            if (pending != null) pending(awayMs);
        }
    }
}
