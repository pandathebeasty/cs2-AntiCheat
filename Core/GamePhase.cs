using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace CS2AntiCheat.Core
{
    /// <summary>
    /// Cheap, cached access to the current round phase via CCSGameRules, plus helpers used by the
    /// detection gates. Detections that rely on precise timing / movement (rapid fire, impossible
    /// yaw, no-recoil, triggerbot, ...) produce false positives during warmup, freeze time, and
    /// round end (respawns, teleports, frozen players, bot takeovers), so the dispatch skips them in
    /// those phases.
    ///
    /// The gamerules entity pointer is resolved once per lookup attempt and cached until the map
    /// changes; all reads are wrapped so a missing entity never throws into the game loop.
    /// </summary>
    internal static class GamePhase
    {
        private static CCSGameRules? cachedRules;

        /// <summary>Drops the cached gamerules handle (call on map start / round restart).</summary>
        internal static void Reset()
        {
            cachedRules = null;
        }

        private static CCSGameRules? GetRules()
        {
            if (cachedRules != null)
            {
                return cachedRules;
            }

            try
            {
                var proxies = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules");
                foreach (var proxy in proxies)
                {
                    if (proxy?.GameRules != null)
                    {
                        cachedRules = proxy.GameRules;
                        return cachedRules;
                    }
                }
            }
            catch
            {
                // entity not ready yet / transient - treat as "unknown phase" (not gated)
            }

            return null;
        }

        /// <summary>True during warmup.</summary>
        internal static bool IsWarmup()
        {
            try { return GetRules()?.WarmupPeriod == true; }
            catch { return false; }
        }

        /// <summary>True during freeze time (buy time at round start, players can't move/shoot freely).</summary>
        internal static bool IsFreezeTime()
        {
            try { return GetRules()?.FreezePeriod == true; }
            catch { return false; }
        }

        /// <summary>
        /// True when timing/movement detections should be SKIPPED for the current phase: warmup or
        /// freeze time. These are the two phases with the most legitimate-but-abnormal behaviour
        /// (frozen players, bot takeovers, respawns, buy-time weapon swaps).
        /// </summary>
        internal static bool ShouldSkipForPhase()
        {
            return IsWarmup() || IsFreezeTime();
        }
    }
}
