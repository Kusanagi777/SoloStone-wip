using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/// <summary>
/// Search telemetry that must never be silent. A beam leaf whose settlement throws an exception the search does not
/// expect (not a <see cref="SimulationFailureException"/>, not a private-history refusal) is scored at -inf and the line
/// is dropped; before phase 7's review that happened with no fault and no fallback recorded. Each such exception is
/// counted here, by type and first message line, so a harness can print the count and require it to be 0.
/// </summary>
internal static class SearchTelemetry
{
    private static int swallowed;
    private static readonly Dictionary<string, int> details = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Leaf exceptions swallowed by the beam since the process started (or the last <see cref="Reset"/>).</summary>
    internal static int SwallowedLeafErrors => Volatile.Read(ref swallowed);

    private static int pendingChoiceLeaves;
    /// <summary>Beam leaves scored as they stand because a pending choice could not be settled (information, not a fault).</summary>
    internal static int PendingChoiceLeaves => Volatile.Read(ref pendingChoiceLeaves);
    internal static void PendingChoiceLeaf() => Interlocked.Increment(ref pendingChoiceLeaves);

    internal static void SwallowedLeaf(Exception error)
    {
        Interlocked.Increment(ref swallowed);
        string key = error.GetType().Name + ": " + (error.Message ?? "").Split('\n')[0];
        lock (details) details[key] = details.TryGetValue(key, out int n) ? n + 1 : 1;
    }

    /// <summary>The swallowed exceptions by "Type: message", most frequent first.</summary>
    internal static IReadOnlyList<KeyValuePair<string, int>> Details()
    {
        lock (details) return details.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).ToList();
    }

    internal static void Reset()
    {
        Interlocked.Exchange(ref swallowed, 0);
        Interlocked.Exchange(ref pendingChoiceLeaves, 0);
        lock (details) details.Clear();
    }
}
