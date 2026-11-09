using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class DoubleTapSaveData
    {
        public bool DetectionEnabled { get; set; } = true;
        public ActionType DetectionAction { get; set; } = ActionType.Kick;

        // How many "two shots from the same gun within <= 1 server tick" pairs before a detection.
        // A doubletap / exploit fires the same weapon twice in the same or adjacent tick, which the
        // engine's fire-rate never allows. Two pairs is already very strong evidence.
        public int RequiredPairs { get; set; } = 2;

        public int MaxDetectionsBeforeAction { get; set; } = 1;
    }

    internal class DoubleTapData
    {
        internal int lastShotTick;
        internal string lastWeapon = string.Empty;
        internal int pairs;
        internal int detections;
    }

    /*
     * Module: DoubleTap (port of the native CS2-AntiCheat doubletap detector)
     * Purpose: A doubletap exploit fires the SAME weapon twice within 0-1 server ticks, which the
     * engine's fire-rate cannot produce. This counts same-weapon shot pairs that land <= 1 tick
     * apart; a few such pairs is the doubletap signature.
     *
     * Complements RapidFire (which compares against NextAttackTick): doubletap is the extreme
     * "same/adjacent tick" case and is cheap and very false-positive resistant.
     */
    internal class DoubleTap : BaseModule
    {
        internal override string Name => "DoubleTap";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private readonly BaseConfig<DoubleTapSaveData> config;
        private readonly DoubleTapData[] playerData;

        internal DoubleTap() : base()
        {
            config = new BaseConfig<DoubleTapSaveData>("DoubleTap");
            playerData = new DoubleTapData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_doubletap_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"DoubleTap Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new DoubleTapData { lastShotTick = -1 };
        }

        internal override void OnPlayerShoot(PlayerData player)
        {
            if (config.Config.DetectionEnabled == false || player.IsBot == true)
            {
                return;
            }

            DoubleTapData data = playerData[player.Index];
            if (data == null)
            {
                return;
            }

            CCSWeaponBaseGun weapon = player.GetWeapon();
            if (weapon == null)
            {
                return;
            }

            string weaponName = weapon.DesignerName ?? string.Empty;
            if (weaponName.Length == 0 || weaponName == "weapon_healthshot")
            {
                return;
            }

            int now = Server.TickCount;

            if (data.lastShotTick < 0)
            {
                data.lastShotTick = now;
                data.lastWeapon = weaponName;
                return;
            }

            int delta = now - data.lastShotTick;
            bool sameWeapon = data.lastWeapon == weaponName;

            data.lastShotTick = now;
            data.lastWeapon = weaponName;

            // A valid doubletap pair: same gun, 0 or 1 ticks apart.
            if (sameWeapon && delta >= 0 && delta <= 1)
            {
                data.pairs++;
                Globals.Log($"{player.Controller.PlayerName}: doubletap pair {data.pairs}/{config.Config.RequiredPairs} ({weaponName}, {delta} tick(s) apart)");

                if (data.pairs >= config.Config.RequiredPairs)
                {
                    OnDoubleTapDetected(player, data);
                    data.pairs = 0;
                }
            }
        }

        private void OnDoubleTapDetected(PlayerData player, DoubleTapData data)
        {
            int max = config.Config.MaxDetectionsBeforeAction;
            data.detections++;

            if (data.detections < max)
            {
                return;
            }

            OnPlayerDetected(player, "DoubleTap");
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
