using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class AntiFlashSaveData
    {
        // Default Log: a skilled player CAN land a kill while partially flashed (spray memory, close
        // range). This flags only kills made while HEAVILY flashed, repeatedly - calibrate first.
        public bool DetectionEnabled { get; set; } = true;
        public ActionType DetectionAction { get; set; } = ActionType.Log;

        // Remaining flash duration (seconds) on the SHOOTER at the moment of a kill, at/above which
        // the shooter is considered "blind". CS2 full flash is ~4-5s; 2.0s means still heavily flashed.
        public float BlindFlashThreshold { get; set; } = 2.0f;

        // How many kills-while-blind (in a streak) before a detection. A no-flash cheat removes the
        // blind effect, so the player keeps killing at full flash; a legit player rarely does.
        public int RequiredBlindKills { get; set; } = 5;

        public int MaxDetectionsBeforeAction { get; set; } = 1;
    }

    internal class AntiFlashData
    {
        internal int blindKills;
        internal int detections;

        internal void Reset()
        {
            blindKills = 0;
        }
    }

    /*
     * Module: Anti-flash / No-flash (heuristic, LOG by default)
     * Purpose: A no-flash cheat cancels the blinding effect, so the player keeps fragging while the
     * server still has them at a high flash duration. This flags a run of kills made while the shooter
     * is heavily flashed (FlashDuration >= threshold).
     *
     * Heuristic -> defaults to Log. A good player can occasionally frag through a partial flash, so
     * the threshold is deliberately high and a STREAK is required. Calibrate before enabling punishment.
     */
    internal class AntiFlash : BaseModule
    {
        internal override string Name => "AntiFlash";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private readonly BaseConfig<AntiFlashSaveData> config;
        private readonly AntiFlashData[] playerData;

        internal AntiFlash() : base()
        {
            config = new BaseConfig<AntiFlashSaveData>("AntiFlash");
            playerData = new AntiFlashData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_antiflash_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"AntiFlash Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new AntiFlashData();
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

        internal override void OnPlayerDead(PlayerData victim, PlayerData shooter)
        {
            if (config.Config.DetectionEnabled == false || shooter.IsBot == true)
            {
                return;
            }

            if (shooter.Index < 0 || shooter.Index >= playerData.Length)
            {
                return;
            }

            AntiFlashData data = playerData[shooter.Index];
            if (data == null)
            {
                return;
            }

            float flash = ReadFlashDuration(shooter);
            if (flash >= config.Config.BlindFlashThreshold)
            {
                data.blindKills++;
                Globals.Log($"{shooter.Controller.PlayerName}: kill while flashed {flash:0.0}s ({data.blindKills}/{config.Config.RequiredBlindKills})");

                if (data.blindKills >= config.Config.RequiredBlindKills)
                {
                    OnAntiFlashDetected(shooter, data);
                    data.blindKills = 0;
                }
            }
            else if (data.blindKills > 0)
            {
                // A normal (not-blind) kill breaks the streak.
                data.blindKills--;
            }
        }

        private void OnAntiFlashDetected(PlayerData player, AntiFlashData data)
        {
            int max = config.Config.MaxDetectionsBeforeAction;
            data.detections++;

            Globals.Log($"{player.Controller.PlayerName}: Suspicious no-flash -> {config.Config.RequiredBlindKills} kills while heavily flashed ({data.detections}/{max} detections)");

            if (data.detections < max)
            {
                return;
            }

            OnPlayerDetected(player, "AntiFlash");
            data.detections = 0;
        }

        /// <summary>Remaining flash duration (seconds) on the player's pawn; 0 on any failure.</summary>
        private static float ReadFlashDuration(PlayerData player)
        {
            try
            {
                return player.Pawn.FlashDuration;
            }
            catch
            {
                return 0f;
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
