namespace LCRanked
{
    public static class ResultReporter
    {
        public static void Report(MatchState match)
        {
            var plugin = Plugin.Instance;
            float elapsed = match.startTimestampMs > 0
                ? (float)((System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - match.startTimestampMs) / 1000.0)
                : 0f;

            plugin.Network.ReportResult(
                match.matchId,
                plugin.LocalPlayerId,
                match.collectedValue,
                match.survived,
                elapsed,
                aliveAt2pm: match.aliveAt2pm,
                aliveAt9pm: match.aliveAt9pm
            );
        }
    }
}
