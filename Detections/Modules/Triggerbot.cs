using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Numerics;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class TriggerbotSaveData
    {
        // Default Log, not Kick/Ban: triggerbot detection is statistical and should be CALIBRATED on
        // your server (watch the logs) before you trust it to punish. Switch to Kick/Ban once you are
        // confident the threshold isn't catching legit players.
        public bool DetectionEnabled { get; set; } = true;
        public ActionType DetectionAction { get; set; } = ActionType.Log;

        // Max angle (degrees) between the shooter's aim and the exact direction to the victim, at the
        // moment of a kill, for the shot to count as "perfectly on target". Very tight.
        public float MaxOnTargetAngle { get; set; } = 1.5f;

        // How many such near-perfect kills (in a row, without a "normal" kill resetting it) before a
        // detection. Triggerbot produces these consistently; a human has spread and imperfect aim.
        public int RequiredPerfectKills { get; set; } = 6;

        // How many detections before an action is taken.
        public int MaxDetectionsBeforeAction { get; set; } = 1;
    }

    internal class TriggerbotData
    {
        internal int perfectKills;
        internal int detections;

        internal void Reset()
        {
            perfectKills = 0;
        }
    }

    /*
     * Module: Triggerbot (heuristic, LOG by default)
     * Purpose: Triggerbots fire the instant the crosshair crosses an enemy, so their kills land with
     * an unnaturally perfect aim-to-target angle, consistently. This module measures the angle between
     * the shooter's eye direction and the direction to the victim at the moment of a kill; a long run
     * of near-zero-angle kills is the triggerbot signature.
     *
     * This is a HEURISTIC and defaults to Log so you can calibrate MaxOnTargetAngle / RequiredPerfectKills
     * against real players before enabling punishment.
     */
    internal class Triggerbot : BaseModule
    {
        internal override string Name => "Triggerbot";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private readonly BaseConfig<TriggerbotSaveData> config;
        private readonly TriggerbotData[] playerData;

        internal Triggerbot() : base()
        {
            config = new BaseConfig<TriggerbotSaveData>("Triggerbot");
            playerData = new TriggerbotData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_triggerbot_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"Triggerbot Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new TriggerbotData();
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

            TriggerbotData data = playerData[shooter.Index];
            if (data == null)
            {
                return;
            }

            float? angle = AimToVictimAngle(shooter, victim);
            if (angle == null)
            {
                return;
            }

            if (angle.Value <= config.Config.MaxOnTargetAngle)
            {
                data.perfectKills++;

                Globals.Log($"{shooter.Controller.PlayerName}: on-target kill {angle.Value:0.00} deg ({data.perfectKills}/{config.Config.RequiredPerfectKills})");

                if (data.perfectKills >= config.Config.RequiredPerfectKills)
                {
                    OnTriggerDetected(shooter, data);
                    data.perfectKills = 0;
                }
            }
            else
            {
                // A normal (imperfect) kill breaks the streak.
                if (data.perfectKills > 0)
                {
                    data.perfectKills--;
                }
            }
        }

        private void OnTriggerDetected(PlayerData player, TriggerbotData data)
        {
            int max = config.Config.MaxDetectionsBeforeAction;
            data.detections++;

            Globals.Log($"{player.Controller.PlayerName}: Suspicious triggerbot -> {config.Config.RequiredPerfectKills} near-perfect kills ({data.detections}/{max} detections)");

            if (data.detections < max)
            {
                return;
            }

            OnPlayerDetected(player, "Triggerbot");
            data.detections = 0;
        }

        /// <summary>
        /// Angle (degrees) between the shooter's eye direction and the direction from the shooter's
        /// eyes to the victim's position, at this moment. Null if positions can't be read.
        /// </summary>
        private static float? AimToVictimAngle(PlayerData shooter, PlayerData victim)
        {
            try
            {
                var shooterPawn = shooter.Pawn;
                var victimPawn = victim.Pawn;
                if (shooterPawn == null || victimPawn == null) return null;

                var eyePos = shooterPawn.AbsOrigin;
                var victimPos = victimPawn.AbsOrigin;
                if (eyePos == null || victimPos == null) return null;

                // Approximate eye height so the direction isn't biased toward the feet.
                var from = new Vector3(eyePos.X, eyePos.Y, eyePos.Z + 64f);
                var to = new Vector3(victimPos.X, victimPos.Y, victimPos.Z + 54f);

                var toVictim = to - from;
                if (toVictim.LengthSquared() < 1f) return null;
                toVictim = Vector3.Normalize(toVictim);

                var aim = ForwardFromAngles(shooterPawn.EyeAngles);

                var dot = Math.Clamp(Vector3.Dot(aim, toVictim), -1f, 1f);
                return (float)(Math.Acos(dot) * (180.0 / Math.PI));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Unit forward vector from pitch/yaw (QAngle). Z = pitch, Y = yaw.</summary>
        private static Vector3 ForwardFromAngles(QAngle angles)
        {
            float pitch = angles.X * (MathF.PI / 180f);
            float yaw = angles.Y * (MathF.PI / 180f);

            float cp = MathF.Cos(pitch);
            return new Vector3(
                cp * MathF.Cos(yaw),
                cp * MathF.Sin(yaw),
                -MathF.Sin(pitch));
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
