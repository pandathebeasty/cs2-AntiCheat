using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class FakeLagSaveData
    {
        // Default Log: fake-lag can overlap with genuine network jitter / packet loss, so CALIBRATE
        // against real players before enabling punishment.
        public bool DetectionEnabled { get; set; } = true;
        public ActionType DetectionAction { get; set; } = ActionType.Log;

        // A "choke" burst is: several ticks where the client processed NO new user command, then a
        // single tick where it processed many at once. This is how fake-lag / choke cheats hold back
        // and dump commands to disrupt lag compensation.
        //
        // Minimum number of consecutive zero-command ticks that must precede a burst.
        public int MinStallTicks { get; set; } = 4;

        // Minimum commands processed in the single "dump" tick after a stall to count as a burst.
        public int MinBurstCommands { get; set; } = 4;

        // How many such choke bursts within the window before a detection is raised.
        public int RequiredBursts { get; set; } = 8;

        // Sliding window (server ticks) over which bursts are counted. Resets after it elapses.
        public int WindowTicks { get; set; } = 320; // ~5s at 64 tick

        public int MaxDetectionsBeforeAction { get; set; } = 2;
    }

    internal class FakeLagData
    {
        internal uint lastCommand;
        internal bool hasLast;
        internal int stallTicks;        // consecutive ticks with 0 new commands
        internal int bursts;            // choke bursts seen in the current window
        internal int windowStartTick;
        internal int detections;
    }

    /*
     * Module: Fake-lag / Choke (heuristic, LOG by default)
     * Purpose: Fake-lag cheats withhold user commands for several ticks and then dump them in one
     * burst to desync the server's view of the player. The signature is a repeating pattern of
     * "several ticks with no processed command, then a tick that processes many". Counted over a
     * window; a high burst count is the fake-lag signature.
     *
     * Heuristic -> defaults to Log. Genuine packet loss can look similar, so calibrate before banning.
     */
    internal class FakeLag : BaseModule
    {
        internal override string Name => "FakeLag";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private readonly BaseConfig<FakeLagSaveData> config;
        private readonly FakeLagData[] playerData;

        internal FakeLag() : base()
        {
            config = new BaseConfig<FakeLagSaveData>("FakeLag");
            playerData = new FakeLagData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_fakelag_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"FakeLag Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new FakeLagData { hasLast = false };
        }

        internal override void OnPlayerTick(PlayerData player)
        {
            if (config.Config.DetectionEnabled == false || player.IsBot == true)
            {
                return;
            }

            FakeLagData data = playerData[player.Index];
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
                data.lastCommand = cmd;
                data.windowStartTick = now;
                return;
            }

            long delta = (long)cmd - data.lastCommand;
            data.lastCommand = cmd;

            // Counter reset (respawn / reconnect) - ignore this sample.
            if (delta < 0)
            {
                data.stallTicks = 0;
                return;
            }

            if (delta == 0)
            {
                // No new command this tick -> client is stalling (choking).
                data.stallTicks++;
            }
            else
            {
                // A dump after a long enough stall is a choke burst.
                if (data.stallTicks >= config.Config.MinStallTicks && delta >= config.Config.MinBurstCommands)
                {
                    data.bursts++;
                }
                data.stallTicks = 0;
            }

            // Evaluate the window.
            if (now - data.windowStartTick >= config.Config.WindowTicks)
            {
                if (data.bursts >= config.Config.RequiredBursts)
                {
                    OnFakeLagDetected(player, data);
                }

                data.bursts = 0;
                data.windowStartTick = now;
            }
        }

        private void OnFakeLagDetected(PlayerData player, FakeLagData data)
        {
            int max = config.Config.MaxDetectionsBeforeAction;
            data.detections++;

            Globals.Log($"{player.Controller.PlayerName}: Suspicious fake-lag -> {data.bursts} choke bursts in window ({data.detections}/{max} detections)");

            if (data.detections < max)
            {
                return;
            }

            OnPlayerDetected(player, "FakeLag");
            data.detections = 0;
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
