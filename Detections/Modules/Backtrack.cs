using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class BacktrackSaveData
    {
        public bool DetectionEnabled { get; set; } = true;

        // IMPORTANT: this is a LOG-ONLY investigation aid, not proof. Backtracking inside the
        // server's lag-compensation window is, by the server's own rules, a valid hit - there is no
        // clean "impossible" signal available server-side in CounterStrikeSharp. This module only
        // flags kills made with abnormally high latency (the window backtrack abuse relies on) so an
        // admin can review the demo. Keep it on Log; do NOT auto-ban from it.
        public ActionType DetectionAction { get; set; } = ActionType.Log;

        // Shooter ping (ms) at the moment of the kill above which the kill is flagged for review.
        // CS2 lag compensation caps around 200 ms, so kills near/above that are the suspicious band.
        public int PingThresholdMs { get; set; } = 150;

        // How many such high-latency kills in a row before it is logged (avoids logging one laggy
        // kill from a player on a genuinely bad connection).
        public int SuspiciousKillsBeforeLog { get; set; } = 4;
    }

    internal class BacktrackData
    {
        internal int suspiciousKills;
    }

    /*
     * Module: Backtrack (LOG-ONLY investigation aid)
     * Purpose: Flag kills made with abnormally high latency, which is the condition backtrack abuse
     *          depends on. This is NOT a reliable backtrack detector - server-side CS2 has no access
     *          to the lag-compensation hitbox history needed to prove backtracking. Treat hits as
     *          leads for manual demo review only.
     */
    internal class Backtrack : BaseModule
    {
        internal override string Name => "Backtrack";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private readonly BaseConfig<BacktrackSaveData> config;
        private readonly BacktrackData[] playerData;

        internal Backtrack() : base()
        {
            config = new BaseConfig<BacktrackSaveData>("Backtrack");
            playerData = new BacktrackData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_backtrack_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"Backtrack Initialized (log-only investigation aid)");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new BacktrackData { suspiciousKills = 0 };
        }

        internal override void OnPlayerDead(PlayerData victim, PlayerData shooter)
        {
            if (config.Config.DetectionEnabled == false || shooter.IsBot == true)
            {
                return;
            }

            // Self-kills / world deaths are not relevant.
            if (shooter.Index == victim.Index)
            {
                return;
            }

            BacktrackData data = playerData[shooter.Index];
            if (data == null)
            {
                return;
            }

            uint ping = shooter.Controller.Ping;

            if (ping < (uint)config.Config.PingThresholdMs)
            {
                // A clean (low-latency) kill resets the streak.
                data.suspiciousKills = 0;
                return;
            }

            data.suspiciousKills++;
            if (data.suspiciousKills >= config.Config.SuspiciousKillsBeforeLog)
            {
                OnPlayerDetected(shooter,
                    $"Backtrack (review) -> {data.suspiciousKills} high-latency kills, ping {ping}ms (>= {config.Config.PingThresholdMs}ms). Manual demo review recommended.");
                data.suspiciousKills = 0;
            }
        }

        private void OnActionCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (CommandHandler.HasPermission(player) == false) return;
            if (command.ArgCount != 2) return;
            if (int.TryParse(command.ArgByIndex(1), out int action) == false) return;
            config.Config.DetectionAction = (ActionType)action;
            config.Save();
        }
    }
}
