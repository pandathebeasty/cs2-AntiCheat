using CounterStrikeSharp.API.Core;
using CS2AntiCheat.Core;

namespace CS2AntiCheat.Detections
{
    internal static class BaseCaller
    {
        /// <summary>
        /// True when timing/movement detections should be skipped right now because of the round
        /// phase (warmup / freeze time). State-management callbacks (join/leave/round) are NOT gated.
        /// </summary>
        private static bool PhaseGated()
        {
            return Globals.SkipDuringWarmupAndFreeze && GamePhase.ShouldSkipForPhase();
        }

        /// <summary>
        /// True when timing-sensitive detections should be vetoed for THIS player because their
        /// connection is unstable (high ping or jitter). Reliable subtick timing / angle deltas are
        /// impossible under a bouncy connection, so acting on a detection there risks false positives.
        /// </summary>
        private static bool NetworkGated(PlayerData player)
        {
            return NetworkSafety.IsUnstable(player);
        }

        internal static void OnPlayerJoin(PlayerData player)
        {
            foreach (BaseModule module in Globals.Modules)
            {
                try
                {
                    module.OnPlayerJoin(player);
                }
                catch (Exception e)
                {
                    Globals.Log($"Exception in {module.Name} -> {e.Message} | {e.StackTrace}");
                }
            }
        }

        internal static void OnPlayerLeave(PlayerData player)
        {
            // Clear the once-per-player punishment guard so the slot can be reused by a new player.
            DetectionHandler.OnPlayerLeave(player);
            NetworkSafety.Reset(player.Controller.Slot);

            foreach (BaseModule module in Globals.Modules)
            {
                try
                {
                    module.OnPlayerLeave(player);
                }
                catch (Exception e)
                {
                    Globals.Log($"Exception in {module.Name} -> {e.Message} | {e.StackTrace}");
                }
            }
        }

        internal static void OnPlayerJump(PlayerData player)
        {
            if (!Globals.Enabled) return;
            if (PhaseGated()) return;
            foreach (BaseModule module in Globals.Modules)
            {
                try
                {
                    module.OnPlayerJump(player);
                }
                catch (Exception e)
                {
                    Globals.Log($"Exception in {module.Name} -> {e.Message} | {e.StackTrace}");
                }
            }
        }

        internal static void OnPlayerHurt(PlayerData victim, PlayerData shooter, HitGroup_t hitgroup)
        {
            if (!Globals.Enabled) return;
            if (PhaseGated()) return;
            foreach (BaseModule module in Globals.Modules)
            {
                try
                {
                    module.OnPlayerHurt(victim, shooter, hitgroup);
                }
                catch (Exception e)
                {
                    Globals.Log($"Exception in {module.Name} -> {e.Message} | {e.StackTrace}");
                }
            }
        }

        internal static void OnPlayerDead(PlayerData victim, PlayerData shooter)
        {
            if (!Globals.Enabled) return;
            foreach (BaseModule module in Globals.Modules)
            {
                try
                {
                    module.OnPlayerDead(victim, shooter);
                }
                catch (Exception e)
                {
                    Globals.Log($"Exception in {module.Name} -> {e.Message} | {e.StackTrace}");
                }
            }
        }

        internal static void OnPlayerShoot(PlayerData player)
        {
            if (!Globals.Enabled) return;
            if (PhaseGated() || NetworkGated(player)) return;
            foreach (BaseModule module in Globals.Modules)
            {
                try
                {
                    module.OnPlayerShoot(player);
                }
                catch (Exception e)
                {
                    Globals.Log($"Exception in {module.Name} -> {e.Message} | {e.StackTrace}");
                }
            }
        }

        internal static void OnPlayerTick(PlayerData player)
        {
            if (!Globals.Enabled) return;

            // Sample ping every tick so the NetworkSafety rolling average/jitter stay fresh. Done
            // before the gate so samples keep flowing even while detections are vetoed.
            NetworkSafety.Record(player);

            if (PhaseGated() || NetworkGated(player)) return;
            foreach (BaseModule module in Globals.Modules)
            {
                try
                {
                    module.OnPlayerTick(player);
                }
                catch (Exception e)
                {
                    Globals.Log($"Exception in {module.Name} -> {e.Message} | {e.StackTrace}");
                }
            }
        }

        internal static void OnRoundStart()
        {
            if (!Globals.Enabled) return;
            foreach (BaseModule module in Globals.Modules)
            {
                try
                {
                    module.OnRoundStart();
                }
                catch (Exception e)
                {
                    Globals.Log($"Exception in {module.Name} -> {e.Message} | {e.StackTrace}");
                }
            }
        }

        internal static void OnRoundEnd()
        {
            if (!Globals.Enabled) return;
            foreach (BaseModule module in Globals.Modules)
            {
                try
                {
                    module.OnRoundEnd();
                }
                catch (Exception e)
                {
                    Globals.Log($"Exception in {module.Name} -> {e.Message} | {e.StackTrace}");
                }
            }
        }

        internal static void OnGameTick()
        {
            if (!Globals.Enabled) return;
            foreach (BaseModule module in Globals.Modules)
            {
                try
                {
                    module.OnGameTick();
                }
                catch(Exception e)
                {
                    Globals.Log($"Exception in {module.Name} -> {e.Message} | {e.StackTrace}");
                }
            }
        }
    }
}
