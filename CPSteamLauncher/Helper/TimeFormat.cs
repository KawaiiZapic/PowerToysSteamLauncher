using System;

namespace CPSteamLauncher.Helper {
    internal class TimeFormat {
        public static string RelativeTimestamp(long ts) {
            var d = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(ts);
            var n = DateTime.UtcNow;
            var span = n - d;
            if (span.TotalSeconds < 15) {
                return Resource.TimeRelativeJustNow;
            } else if (span.TotalMinutes < 1) {
                return String.Format(Resource.TimeRelativeSec, Math.Round(span.TotalSeconds));

            } else if (span.TotalHours < 1) {
                return string.Format(Resource.TimeRelativeMin, Math.Round(span.TotalMinutes));

            } else if (span.TotalDays < 1) {
                return String.Format(Resource.TimeRelativeHour, Math.Round(span.TotalHours));

            } else if (span.TotalDays < 30) {
                return String.Format(Resource.TimeRelativeDay, Math.Round(span.TotalDays));

            } else {
                return d.ToLocalTime().ToString();
            }
        }

        public static string RelativeTime(long seconds) {
            if (seconds <= 0) {
                return Resource.TimeAbsNever;
            }
            var span = TimeSpan.FromSeconds(seconds);
            if (span.TotalMinutes < 1) {
                return String.Format(Resource.TimeAbsSec, Math.Round(span.TotalSeconds));

            } else if (span.TotalHours < 1) {
                return String.Format(Resource.TimeAbsMin, Math.Round(span.TotalMinutes));

            } else {
                return String.Format(Resource.TimeAbsHour, Math.Round(span.TotalHours));

            }
        }
    }
}
