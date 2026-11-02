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
        /// True when timing-sensitive detections should be skipped for THIS player because their ping
        /// is too high for reliable subtick timing / angle deltas. 0 disables the gate.
        /// </summary>
        private static bool PingGated(PlayerData player)
        {
            var max = Globals.MaxPingForTimingChecks;
            if (max <= 0 || player.IsBot)
            {
                return false;
            }

            try { return player.Controller.Ping >= (uint)max; }
            catch { return false; }
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
            if (PhaseGated() || PingGated(player)) return;
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
            if (PhaseGated() || PingGated(player)) return;
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
