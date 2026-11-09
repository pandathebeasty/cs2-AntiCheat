using CounterStrikeSharp.API;

namespace CS2AntiCheat.Core
{
    /// <summary>
    /// Per-player network quality tracking used to "veto" timing/aim detections when a player's
    /// connection is unstable. Subtick timing and per-tick angle deltas become unreliable under high
    /// ping or jitter, which is a major false-positive source - so a detection raised while the
    /// network is unstable should be treated as "review", not an automatic ban.
    ///
    /// Only values CounterStrikeSharp exposes reliably are used: the controller's Ping, from which a
    /// rolling average and jitter (mean absolute deviation) are computed. This is a safe superset of
    /// the old single "max ping" gate; packet loss / choke are intentionally NOT read here because
    /// CSSharp has no stable API for them (reading them natively would risk crashing the server).
    /// </summary>
    internal static class NetworkSafety
    {
        private const int MaxSamples = 32; // ~0.5s of ping samples at 64 tick if sampled each tick

        private sealed class Sample
        {
            internal readonly int[] pings = new int[MaxSamples];
            internal int count;
            internal int head;
        }

        private static readonly Sample[] samples = new Sample[80];

        internal static void Reset(int slot)
        {
            if (slot >= 0 && slot < samples.Length)
            {
                samples[slot] = null!;
            }
        }

        /// <summary>Records a fresh ping sample for the player (call from the per-player tick).</summary>
        internal static void Record(PlayerData player)
        {
            if (player.IsBot)
            {
                return;
            }

            int slot = player.Controller.Slot;
            if (slot < 0 || slot >= samples.Length)
            {
                return;
            }

            uint ping;
            try { ping = player.Controller.Ping; }
            catch { return; }

            var s = samples[slot] ??= new Sample();
            s.pings[s.head] = (int)ping;
            s.head = (s.head + 1) % MaxSamples;
            if (s.count < MaxSamples)
            {
                s.count++;
            }
        }

        /// <summary>Rolling average ping (ms). 0 if no samples.</summary>
        internal static int AveragePing(int slot)
        {
            if (slot < 0 || slot >= samples.Length || samples[slot] is not { } s || s.count == 0)
            {
                return 0;
            }

            long sum = 0;
            for (int i = 0; i < s.count; i++)
            {
                sum += s.pings[i];
            }
            return (int)(sum / s.count);
        }

        /// <summary>Ping jitter (mean absolute deviation from the average, ms). 0 if too few samples.</summary>
        internal static int Jitter(int slot)
        {
            if (slot < 0 || slot >= samples.Length || samples[slot] is not { } s || s.count < 4)
            {
                return 0;
            }

            int avg = AveragePing(slot);
            long dev = 0;
            for (int i = 0; i < s.count; i++)
            {
                dev += Math.Abs(s.pings[i] - avg);
            }
            return (int)(dev / s.count);
        }

        /// <summary>
        /// True when the player's connection is unstable enough that timing/aim detections should be
        /// vetoed. Driven by the General.json thresholds mirrored into Globals.
        /// </summary>
        internal static bool IsUnstable(PlayerData player)
        {
            if (player.IsBot)
            {
                return false;
            }

            int slot = player.Controller.Slot;
            int maxPing = Globals.MaxPingForTimingChecks;
            int maxJitter = Globals.MaxJitterForTimingChecks;

            if (maxPing > 0 && AveragePing(slot) >= maxPing)
            {
                return true;
            }

            if (maxJitter > 0 && Jitter(slot) >= maxJitter)
            {
                return true;
            }

            return false;
        }

        /// <summary>Human-readable network snapshot for a detection's evidence / log line.</summary>
        internal static string Describe(PlayerData player)
        {
            int slot = player.Controller.Slot;
            return $"ping {AveragePing(slot)}ms, jitter {Jitter(slot)}ms";
        }
    }
}
