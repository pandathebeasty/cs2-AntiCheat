using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class DuckSpamSaveData
    {
        public bool DetectionEnabled { get; set; } = true;
        public ActionType DetectionAction { get; set; } = ActionType.Kick;

        // Maximum duck<->unduck toggles allowed within the sampling window. A human cannot toggle
        // crouch dozens of times per second; fake-duck anti-aim scripts do. Conservative default.
        public int MaxToggles { get; set; } = 10;

        // Sampling window in server ticks (64 tick => 64 ticks ~= 1 second).
        public int WindowTicks { get; set; } = 64;
    }

    internal class DuckSpamData
    {
        internal bool lastDuck;
        internal bool hasLast;
        internal int toggles;
        internal int windowStartTick;
    }

    /*
     * Module: Duck Spam (fake-duck)
     * Purpose: Flag crouch toggling faster than any human input can produce.
     * Safe: counts physical toggle rate over a window; legitimate crouch use stays far below.
     */
    internal class DuckSpam : BaseModule
    {
        internal override string Name => "DuckSpam";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private const uint DuckButton = 4; // PlayerButtons.Duck

        private readonly BaseConfig<DuckSpamSaveData> config;
        private readonly DuckSpamData[] playerData;

        internal DuckSpam() : base()
        {
            config = new BaseConfig<DuckSpamSaveData>("DuckSpam");
            playerData = new DuckSpamData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_duckspam_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"DuckSpam Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new DuckSpamData { hasLast = false, toggles = 0, windowStartTick = Server.TickCount };
        }

        internal override void OnPlayerTick(PlayerData player)
        {
            if (config.Config.DetectionEnabled == false || player.IsBot == true)
            {
                return;
            }

            DuckSpamData data = playerData[player.Index];
            if (data == null)
            {
                return;
            }

            var movement = player.Pawn.MovementServices;
            if (movement == null)
            {
                return;
            }

            bool ducking = (movement.Buttons.ButtonStates[0] & DuckButton) != 0;

            if (data.hasLast == false)
            {
                data.lastDuck = ducking;
                data.hasLast = true;
                return;
            }

            if (ducking != data.lastDuck)
            {
                data.toggles++;
                data.lastDuck = ducking;
            }

            int now = Server.TickCount;
            if (now - data.windowStartTick >= config.Config.WindowTicks)
            {
                if (data.toggles > config.Config.MaxToggles)
                {
                    OnPlayerDetected(player, $"DuckSpam -> {data.toggles} toggles in {config.Config.WindowTicks} ticks (limit {config.Config.MaxToggles})");
                }

                data.toggles = 0;
                data.windowStartTick = now;
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
