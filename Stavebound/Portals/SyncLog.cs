using System.Collections.Generic;
using System.Linq;
using Stavebound.Config;

namespace Stavebound.Portals
{
    /// <summary>
    /// Narrates the portal registry's sync.
    /// <para>
    /// The registry is the one part of this mod that cannot be tested alone: single player is its own
    /// server, so the RPC path never runs and the interesting failure — a client believing something
    /// different from the server — is invisible until two machines disagree in front of you. Testing
    /// that by reproducing symptoms is slow and needs two people; reading it off a log needs one, and
    /// can be done after the fact.
    /// </para>
    /// <para>
    /// Everything here is gated on <see cref="StaveboundConfig.LogNetworkSync"/> and writes at info
    /// level so it appears without turning on BepInEx debug logging. It is a development aid and
    /// defaults off before release.
    /// </para>
    /// </summary>
    internal static class SyncLog
    {
        private static bool Enabled => StaveboundConfig.LogNetworkSync?.Value ?? false;

        internal static void Say(string message)
        {
            if (Enabled)
            {
                Jotunn.Logger.LogInfo($"[sync] {message}");
            }
        }

        /// <summary>
        /// Always logged, gate or no gate: something has gone wrong that a player will notice, and a
        /// diagnostic switch should not decide whether they can find out why.
        /// </summary>
        internal static void Warn(string message)
        {
            Jotunn.Logger.LogWarning($"[sync] {message}");
        }

        /// <summary>
        /// What changed between two versions of the registry, in the terms a person debugging it
        /// actually asks in: what appeared, what vanished, what now points somewhere else.
        /// <para>
        /// A bare count tells you the two sides disagree. This tells you which portal to go and look
        /// at, which is the difference between a five-minute investigation and an afternoon.
        /// </para>
        /// </summary>
        internal static string Difference(IReadOnlyList<PortalRecord> before, IReadOnlyList<PortalRecord> after)
        {
            // Indexed rather than ToDictionary: that throws on a duplicate key, and duplicate pids
            // are the exact condition EnsurePid exists to repair. A diagnostic that crashes when the
            // thing it diagnoses goes wrong is worse than no diagnostic.
            var old = new Dictionary<long, PortalRecord>();
            foreach (PortalRecord record in before)
            {
                old[record.Pid] = record;
            }

            var added = new List<string>();
            var retargeted = new List<string>();
            var renamed = new List<string>();

            foreach (PortalRecord now in after)
            {
                if (!old.TryGetValue(now.Pid, out PortalRecord was))
                {
                    added.Add(now.ToString());
                    continue;
                }

                if (was.TargetPid != now.TargetPid)
                {
                    retargeted.Add($"{now} -> {Name(after, now.TargetPid)}");
                }

                if (was.Name != now.Name)
                {
                    renamed.Add($"{was} is now {now}");
                }

                old.Remove(now.Pid);
            }

            var parts = new List<string>();
            if (added.Count > 0)
            {
                parts.Add($"added {string.Join(", ", added)}");
            }

            // Whatever is left in `old` was not matched by anything in `after`.
            if (old.Count > 0)
            {
                parts.Add($"gone {string.Join(", ", old.Values.Select(p => p.ToString()))}");
            }

            if (retargeted.Count > 0)
            {
                parts.Add($"re-aimed {string.Join(", ", retargeted)}");
            }

            if (renamed.Count > 0)
            {
                parts.Add($"renamed {string.Join(", ", renamed)}");
            }

            return parts.Count == 0 ? "no visible difference" : string.Join("; ", parts);
        }

        /// <summary>Resolves a target pid for display, saying so plainly when it cannot.</summary>
        internal static string Name(IReadOnlyList<PortalRecord> among, long pid)
        {
            if (pid == PortalTarget.NoPid)
            {
                return "vanilla tag pairing";
            }

            foreach (PortalRecord candidate in among)
            {
                if (candidate.Pid == pid)
                {
                    return candidate.ToString();
                }
            }

            return $"an unknown portal (pid {pid})";
        }
    }
}
