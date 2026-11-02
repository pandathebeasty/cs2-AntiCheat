using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class SpinbotSaveData
    {
        public bool DetectionEnabled { get; set; } = true;
        public ActionType DetectionAction { get; set; } = ActionType.Kick;

        // Minimum absolute yaw change per tick (degrees) that counts as "spinning". A human flick is
        // a brief burst; a spinbot sustains a large per-tick delta. 20 deg/tick at 64 tick = ~1280
        // deg/s, well beyond human aim.
        public float MinYawDeltaPerTick { get; set; } = 20f;

        // How many CONSECUTIVE ticks of >= MinYawDeltaPerTick (all in a consistent direction, or
        // alternating hard) are required before it's flagged. Sustained rotation, not a single flick.
        public int MinConsecutiveSpinTicks { get; set; } = 12;

        // How many separate spin bursts before an action is taken.
        public int MaxDetectionsBeforeAction { get; set; } = 2;
    }

    internal class SpinbotData
    {
        internal float lastYaw;
        internal bool hasLastYaw;
        internal int consecutiveSpinTicks;
        internal int detections;

        internal void Reset()
        {
            hasLastYaw = false;
            consecutiveSpinTicks = 0;
        }
    }

    /*
     * Module: Spinbot
     * Purpose: Detect a sustained, inhuman yaw rotation (classic spinbot). Unlike the Aimbot module -
     * which flags a single large flick - this looks for a LARGE per-tick yaw delta sustained over many
     * consecutive ticks, which no human input can produce. That makes it very false-positive resistant.
     */
    internal class Spinbot : BaseModule
    {
        internal override string Name => "Spinbot";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private readonly BaseConfig<SpinbotSaveData> config;
        private readonly SpinbotData[] playerData;

        internal Spinbot() : base()
        {
            config = new BaseConfig<SpinbotSaveData>("Spinbot");
            playerData = new SpinbotData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_spinbot_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"Spinbot Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new SpinbotData();
        }

        internal override void OnRoundStart()
        {
            foreach (PlayerData player in Globals.Players)
            {
                if (player == null || player.IsBot == true)
                {
                    continue;
                }

                playerData[player.Index]?.Reset();
            }
        }

        internal override void OnPlayerTick(PlayerData player)
        {
            if (config.Config.DetectionEnabled == false || player.IsBot == true)
            {
                return;
            }

            SpinbotData data = playerData[player.Index];
            if (data == null)
            {
                return;
            }

            float yaw = player.Pawn.EyeAngles.Y;

            if (data.hasLastYaw == false)
            {
                data.lastYaw = yaw;
                data.hasLastYaw = true;
                return;
            }

            // Shortest signed angular difference, normalized to [-180, 180].
            float delta = yaw - data.lastYaw;
            while (delta > 180f) delta -= 360f;
            while (delta < -180f) delta += 360f;
            data.lastYaw = yaw;

            if (MathF.Abs(delta) >= config.Config.MinYawDeltaPerTick)
            {
                data.consecutiveSpinTicks++;

                if (data.consecutiveSpinTicks >= config.Config.MinConsecutiveSpinTicks)
                {
                    OnSpinDetected(player, data);
                    data.consecutiveSpinTicks = 0;
                }
            }
            else
            {
                // A single calm tick ends the "sustained" run.
                data.consecutiveSpinTicks = 0;
            }
        }

        private void OnSpinDetected(PlayerData player, SpinbotData data)
        {
            int max = config.Config.MaxDetectionsBeforeAction;
            data.detections++;

            Globals.Log($"{player.Controller.PlayerName}: Suspicious spinbot -> {data.consecutiveSpinTicks} sustained spin ticks ({data.detections}/{max} detections)");

            if (data.detections < max)
            {
                return;
            }

            OnPlayerDetected(player, "Spinbot");
            data.detections = 0;
        }

        // ----- Commands ----- \\

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
