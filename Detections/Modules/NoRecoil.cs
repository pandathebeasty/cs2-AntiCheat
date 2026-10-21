using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class NoRecoilSaveData
    {
        public bool DetectionEnabled { get; set; } = true;

        // Per your request this defaults to Ban. NOTE: server-side no-recoil detection is inherently
        // less certain than the physical-impossibility modules; consider running it on Log (1) first
        // and watching for false positives before leaving it on Ban (3).
        public ActionType DetectionAction { get; set; } = ActionType.Ban;

        // Minimum consecutive full-auto shots in one burst before the burst is evaluated. Long
        // bursts are where recoil compensation is unmistakable, so a high value avoids tap/burst FPs.
        public int MinBurstShots { get; set; } = 12;

        // Aim-punch magnitude (degrees) that must have accumulated over the burst for it to count as
        // "recoil was clearly applied". Below this the engine barely kicked, so we skip (no verdict).
        public float MinAccumulatedPunch { get; set; } = 4.0f;

        // Maximum total vertical view movement (degrees) across the burst for it to be considered
        // "not compensating". A legit player pulls down noticeably against spray; a no-recoil cheat
        // leaves the view almost still. Conservative so only near-zero compensation trips it.
        public float MaxPitchCompensation { get; set; } = 1.0f;

        public int MaxDetectionsBeforeAction { get; set; } = 2;
    }

    internal class NoRecoilData
    {
        internal int burstShots;
        internal float burstStartPitch;
        internal float lastPunch;
        internal float accumulatedPunch;
        internal int lastShotTick;
        internal int detections;
    }

    /*
     * Module: No Recoil (conservative)
     * Purpose: Flag sustained full-auto bursts where clear recoil (aim punch) was applied but the
     *          player's view barely moved to compensate - the signature of recoil removal.
     * CAUTION: This is the least certain module (unlike the pure-physics ones). Thresholds are
     *          deliberately conservative; prefer Log first on a live server.
     */
    internal class NoRecoil : BaseModule
    {
        internal override string Name => "NoRecoil";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private const int BurstGapTicks = 16; // >~0.25s between shots ends the burst

        private readonly BaseConfig<NoRecoilSaveData> config;
        private readonly NoRecoilData[] playerData;

        internal NoRecoil() : base()
        {
            config = new BaseConfig<NoRecoilSaveData>("NoRecoil");
            playerData = new NoRecoilData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_norecoil_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"NoRecoil Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new NoRecoilData();
        }

        internal override void OnPlayerShoot(PlayerData player)
        {
            if (config.Config.DetectionEnabled == false || player.IsBot == true)
            {
                return;
            }

            NoRecoilData data = playerData[player.Index];
            if (data == null)
            {
                return;
            }

            int now = Server.TickCount;
            float pitch = player.Pawn.EyeAngles.X;
            float punch = GetPunchMagnitude(player);

            // A gap between shots ends the previous burst; start a fresh one.
            if (data.burstShots == 0 || now - data.lastShotTick > BurstGapTicks)
            {
                StartBurst(data, now, pitch, punch);
                return;
            }

            data.burstShots++;
            data.lastShotTick = now;

            // Accumulate how much recoil (aim punch) the engine applied during the burst.
            float punchDelta = MathF.Abs(punch - data.lastPunch);
            data.accumulatedPunch += punchDelta;
            data.lastPunch = punch;

            // Only evaluate once the burst is long enough to be unambiguous.
            if (data.burstShots < config.Config.MinBurstShots)
            {
                return;
            }

            // How much the player actually moved their view vertically across the burst.
            float pitchCompensation = MathF.Abs(pitch - data.burstStartPitch);

            bool recoilClearlyApplied = data.accumulatedPunch >= config.Config.MinAccumulatedPunch;
            bool noCompensation = pitchCompensation <= config.Config.MaxPitchCompensation;

            if (recoilClearlyApplied && noCompensation)
            {
                data.detections++;
                if (data.detections >= config.Config.MaxDetectionsBeforeAction)
                {
                    OnPlayerDetected(player,
                        $"NoRecoil -> burst {data.burstShots} shots, punch {data.accumulatedPunch:F2} deg, view moved {pitchCompensation:F2} deg");
                    data.detections = 0;
                }

                // Reset the burst so one long spray is counted once.
                StartBurst(data, now, pitch, punch);
            }
        }

        private static void StartBurst(NoRecoilData data, int tick, float pitch, float punch)
        {
            data.burstShots = 1;
            data.burstStartPitch = pitch;
            data.lastPunch = punch;
            data.accumulatedPunch = 0f;
            data.lastShotTick = tick;
        }

        /// <summary>Magnitude of the applied aim punch (recoil kick) in degrees, 0 if unavailable.</summary>
        private static float GetPunchMagnitude(PlayerData player)
        {
            var punchServices = player.Pawn.AimPunchServices;
            if (punchServices == null)
            {
                return 0f;
            }

            var angle = punchServices.PredictableBaseAngle;
            return MathF.Sqrt((angle.X * angle.X) + (angle.Y * angle.Y));
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
