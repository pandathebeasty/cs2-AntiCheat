using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class ImpossibleYawSaveData
    {
        public bool DetectionEnabled { get; set; } = true;
        public ActionType DetectionAction { get; set; } = ActionType.Kick;

        // Maximum yaw change (degrees) allowed between two consecutive ticks. A human mouse flick
        // tops out well below a full continuous spin; spinbots rotate far faster. 120 deg/tick at
        // 64 tick = ~7680 deg/s, which no legitimate input produces continuously.
        public float MaxYawDeltaPerTick { get; set; } = 120f;

        // Consecutive over-limit ticks before acting (a spinbot trips this every tick; a one-off
        // teleport/respawn angle snap will not sustain it).
        public int MaxDetectionsBeforeAction { get; set; } = 6;
    }

    internal class ImpossibleYawData
    {
        internal float lastYaw;
        internal bool hasLast;
        internal int detections;
    }

    /*
     * Module: Impossible Yaw (spinbot)
     * Purpose: Flag horizontal view rotation faster than any legitimate input can sustain.
     * Safe: measures a physical impossibility (sustained per-tick yaw delta), not a statistic.
     */
    internal class ImpossibleYaw : BaseModule
    {
        internal override string Name => "ImpossibleYaw";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private readonly BaseConfig<ImpossibleYawSaveData> config;
        private readonly ImpossibleYawData[] playerData;

        internal ImpossibleYaw() : base()
        {
            config = new BaseConfig<ImpossibleYawSaveData>("ImpossibleYaw");
            playerData = new ImpossibleYawData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_impossibleyaw_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"ImpossibleYaw Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new ImpossibleYawData { hasLast = false, detections = 0 };
        }

        internal override void OnPlayerTick(PlayerData player)
        {
            if (config.Config.DetectionEnabled == false || player.IsBot == true)
            {
                return;
            }

            ImpossibleYawData data = playerData[player.Index];
            if (data == null)
            {
                return;
            }

            float yaw = player.Pawn.EyeAngles.Y;

            if (data.hasLast == false)
            {
                data.lastYaw = yaw;
                data.hasLast = true;
                return;
            }

            // Shortest angular distance between the two yaws (handles the -180/180 wrap).
            float delta = MathF.Abs(yaw - data.lastYaw) % 360f;
            if (delta > 180f)
            {
                delta = 360f - delta;
            }

            data.lastYaw = yaw;

            if (delta > config.Config.MaxYawDeltaPerTick)
            {
                data.detections++;
                if (data.detections >= config.Config.MaxDetectionsBeforeAction)
                {
                    OnPlayerDetected(player, $"ImpossibleYaw -> {delta:F1} deg/tick (limit {config.Config.MaxYawDeltaPerTick})");
                    data.detections = 0;
                }
            }
            else
            {
                // Decay so isolated spikes (teleport, respawn) do not accumulate into a ban.
                if (data.detections > 0)
                {
                    data.detections--;
                }
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
