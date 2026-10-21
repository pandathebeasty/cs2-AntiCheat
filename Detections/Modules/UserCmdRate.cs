using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class UserCmdRateSaveData
    {
        public bool DetectionEnabled { get; set; } = true;
        public ActionType DetectionAction { get; set; } = ActionType.Kick;

        // Max user commands a client may process per server tick on average over the window. The
        // engine normally processes ~1 usercmd per tick; sustained values well above that indicate
        // command flooding / choke exploits. 2.0 leaves generous headroom for legit jitter.
        public float MaxCommandsPerTick { get; set; } = 2.0f;

        // Sampling window in server ticks before evaluating the average.
        public int WindowTicks { get; set; } = 64;

        public int MaxDetectionsBeforeAction { get; set; } = 3;
    }

    internal class UserCmdRateData
    {
        internal uint lastCommandNumber;
        internal bool hasLast;
        internal int windowStartTick;
        internal uint windowStartCommand;
        internal int detections;
    }

    /*
     * Module: UserCmd Rate
     * Purpose: Flag clients processing far more user commands than ticks (command flooding).
     * Safe-ish: uses LastCommandNumberProcessed vs ticks elapsed; conservative threshold.
     */
    internal class UserCmdRate : BaseModule
    {
        internal override string Name => "UserCmdRate";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private readonly BaseConfig<UserCmdRateSaveData> config;
        private readonly UserCmdRateData[] playerData;

        internal UserCmdRate() : base()
        {
            config = new BaseConfig<UserCmdRateSaveData>("UserCmdRate");
            playerData = new UserCmdRateData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_usercmdrate_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"UserCmdRate Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new UserCmdRateData { hasLast = false, detections = 0 };
        }

        internal override void OnPlayerTick(PlayerData player)
        {
            if (config.Config.DetectionEnabled == false || player.IsBot == true)
            {
                return;
            }

            UserCmdRateData data = playerData[player.Index];
            if (data == null)
            {
                return;
            }

            var movement = player.Pawn.MovementServices;
            if (movement == null)
            {
                return;
            }

            uint cmd = movement.LastCommandNumberProcessed;
            int now = Server.TickCount;

            if (data.hasLast == false)
            {
                data.hasLast = true;
                data.lastCommandNumber = cmd;
                data.windowStartTick = now;
                data.windowStartCommand = cmd;
                return;
            }

            data.lastCommandNumber = cmd;

            int ticksElapsed = now - data.windowStartTick;
            if (ticksElapsed < config.Config.WindowTicks)
            {
                return;
            }

            // Commands processed across the window vs ticks that passed.
            long commandsProcessed = (long)cmd - data.windowStartCommand;
            data.windowStartTick = now;
            data.windowStartCommand = cmd;

            // Ignore nonsensical windows (counter reset on respawn/reconnect).
            if (commandsProcessed <= 0 || ticksElapsed <= 0)
            {
                return;
            }

            float perTick = (float)commandsProcessed / ticksElapsed;
            if (perTick > config.Config.MaxCommandsPerTick)
            {
                data.detections++;
                if (data.detections >= config.Config.MaxDetectionsBeforeAction)
                {
                    OnPlayerDetected(player, $"UserCmdRate -> {perTick:F2} cmds/tick over {ticksElapsed} ticks (limit {config.Config.MaxCommandsPerTick})");
                    data.detections = 0;
                }
            }
            else if (data.detections > 0)
            {
                data.detections--;
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
