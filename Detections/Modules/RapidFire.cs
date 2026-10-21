using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class RapidFireSaveData
    {
        public bool DetectionEnabled { get; set; } = true;
        public ActionType DetectionAction { get; set; } = ActionType.Kick;

        public int MaxDetectionsBeforeAction { get; set; } = 5;
    }

    internal class RapidFireData
    {
        internal int lastBulletShotTick;
        internal int lastNextAttackTick;
        internal int rapidDetections;
    }

    /*
     * Module: Rapid Fire
     * Purpose: Detect rapid fire where weapons can fire at speeds that are not normal, either within the same tick or the ticks after
     * NOTE: This is not production ready. It needs a lot of testing so keep that in mind when using this
     */
    internal class RapidFire : BaseModule
    {
        internal override string Name => "RapidFire";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private readonly BaseConfig<RapidFireSaveData> config;
        private readonly RapidFireData[] playerData;

        internal RapidFire() : base()
        {
            config = new BaseConfig<RapidFireSaveData>("RapidFire");
            playerData = new RapidFireData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_rapidfire_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"RapidFire Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new RapidFireData()
            {
                lastBulletShotTick = 0,
                lastNextAttackTick = 0,
                rapidDetections = 0
            };
        }

        internal override void OnPlayerShoot(PlayerData player)
        {
            if (config.Config.DetectionEnabled == false)
            {
                return;
            }

            if (player.IsBot == true)
            {
                return;
            }

            CCSWeaponBaseGun weapon = player.GetWeapon();
            string weaponName = weapon.DesignerName;

            if (weaponName == "weapon_healthshot")
            {
                return;
            }

            int nextAttack;
            switch (weapon.WeaponMode)
            {
                case CSWeaponMode.Primary_Mode: nextAttack = weapon.NextPrimaryAttackTick; break;
                case CSWeaponMode.Secondary_Mode: nextAttack = weapon.NextSecondaryAttackTick; break;
                default: Globals.Log($"Unsupported WeaponMode: {(int)weapon.WeaponMode} on {weaponName}"); return;
            }

            int serverTickCount = Server.TickCount;
            RapidFireData data = playerData[player.Index];

            // Authoritative check: the engine sets NextPrimary/SecondaryAttackTick to the earliest
            // tick the weapon is ALLOWED to fire again. A shot landing strictly before that tick is
            // physically impossible without manipulating the fire rate. This is far more reliable
            // than the previous "two shots one tick apart" heuristic (which flagged legit fast guns).
            //
            // nextAttack was captured AFTER this shot, so it already points at the next allowed tick.
            // We compare the gap since the previous shot to how long the previous shot's cooldown
            // should have been. A conservative 1-tick tolerance absorbs rounding / subtick timing.
            int tickDiff = serverTickCount - data.lastBulletShotTick;

            // Reset the burst tracking after a long pause between shots.
            if (tickDiff > 64)
            {
                data.lastBulletShotTick = serverTickCount;
                data.lastNextAttackTick = nextAttack;
                data.rapidDetections = 0;
                return;
            }

            // If this shot occurred before the previously-promised next-allowed tick (with a 1-tick
            // tolerance), the weapon fired faster than the engine permits.
            bool firedTooEarly = data.lastNextAttackTick > 0 && serverTickCount < data.lastNextAttackTick - 1;

            if (firedTooEarly)
            {
                data.rapidDetections++;
                Globals.Log($"RapidFire suspect -> {player.Controller.PlayerName} | {weaponName} | shotTick {serverTickCount} < allowed {data.lastNextAttackTick} | gap {tickDiff}");
            }
            else if (data.rapidDetections > 0)
            {
                // Decay so an occasional timing blip never accumulates into a ban.
                data.rapidDetections--;
            }

            if (data.rapidDetections >= config.Config.MaxDetectionsBeforeAction)
            {
                OnPlayerDetected(player, $"RapidFire -> {weaponName} fired faster than engine cooldown ({data.rapidDetections} times)");
                data.rapidDetections = 0;
            }

            data.lastBulletShotTick = serverTickCount;
            data.lastNextAttackTick = nextAttack;
        }

        // ----- Commands ----- \\

        private void OnActionCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (CommandHandler.HasPermission(player) == false)
            {
                return;
            }

            if (command.ArgCount != 2)
            {
                return;
            }

            string arg = command.ArgByIndex(1);
            if (int.TryParse(arg, out int action) == false)
            {
                return;
            }

            ActionType actionType = (ActionType)action;
            if (config.Config.DetectionAction.HasFlag(actionType) == false)
            {
                return;
            }

            config.Config.DetectionAction = actionType;
            config.Save();
        }
    }
}
