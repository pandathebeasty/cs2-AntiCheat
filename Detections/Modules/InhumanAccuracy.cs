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
    public class InhumanAccuracySaveData
    {
        // Default Log: this is a statistical detection over a long window. Calibrate against real
        // players before enabling punishment.
        public bool DetectionEnabled { get; set; } = true;
        public ActionType DetectionAction { get; set; } = ActionType.Log;

        // Minimum "aim attempts" (shots fired while the crosshair was already on an enemy, far enough
        // away to matter) in the window before accuracy is evaluated. High volume = reliable sample.
        public int MinimumAttempts { get; set; } = 40;

        // Required hit percentage across those attempts to flag. Humans spread shots; a closet/legit
        // aimbot lands almost every on-target shot.
        public int RequiredAccuracyPercent { get; set; } = 90;

        // Minimum distance (game units) to the enemy for a shot to count as an "attempt" (ignores
        // point-blank spray where everyone is accurate).
        public float MinimumDistance { get; set; } = 300f;

        // Angular tolerance (degrees) for "crosshair was on the enemy" when the shot was fired.
        public float OnTargetAngle { get; set; } = 3.0f;

        // Rolling evidence window in seconds.
        public int WindowSeconds { get; set; } = 300;

        public int MaxDetectionsBeforeAction { get; set; } = 1;
    }

    internal class InhumanAccuracyData
    {
        // Timestamped attempts/hits so the window can be pruned.
        internal readonly Queue<(double time, bool hit)> events = new();
        internal int detections;

        // Set true by OnPlayerHurt for the shooter in the same tick as a counted attempt.
        internal bool pendingHit;
    }

    /*
     * Module: InhumanAccuracy (heuristic, LOG by default; ported idea from native CS2-AntiCheat)
     * Purpose: Count shots fired while the crosshair was already inside a player-sized area around a
     * far-enough enemy ("aim attempts"), and how many dealt damage. A very high hit rate over a large
     * number of attempts is beyond human aim.
     *
     * Simplified vs the native shot-correlator: it evaluates aim at fire time against currently-living
     * enemies, not against a historical lag-comp frame. That makes it conservative (a genuine aimbot
     * still trivially exceeds the threshold) and false-positive resistant thanks to the high volume
     * requirement. Heuristic -> defaults to Log.
     */
    internal class InhumanAccuracy : BaseModule
    {
        internal override string Name => "InhumanAccuracy";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private readonly BaseConfig<InhumanAccuracySaveData> config;
        private readonly InhumanAccuracyData[] playerData;

        internal InhumanAccuracy() : base()
        {
            config = new BaseConfig<InhumanAccuracySaveData>("InhumanAccuracy");
            playerData = new InhumanAccuracyData[Server.MaxPlayers];

            CommandHandler.RegisterCommand("tbac_inhumanaccuracy_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            Globals.Log($"InhumanAccuracy Initialized");
        }

        internal override void OnPlayerJoin(PlayerData player)
        {
            if (player.IsBot == true)
            {
                return;
            }

            playerData[player.Index] = new InhumanAccuracyData();
        }

        internal override void OnPlayerHurt(PlayerData victim, PlayerData shooter, HitGroup_t hitgroup)
        {
            if (config.Config.DetectionEnabled == false || shooter.IsBot == true)
            {
                return;
            }

            if (shooter.Index < 0 || shooter.Index >= playerData.Length)
            {
                return;
            }

            // Mark that the shooter's most recent shot dealt damage; consumed by the next shot sample.
            var data = playerData[shooter.Index];
            if (data != null)
            {
                data.pendingHit = true;
            }
        }

        internal override void OnPlayerShoot(PlayerData player)
        {
            if (config.Config.DetectionEnabled == false || player.IsBot == true)
            {
                return;
            }

            InhumanAccuracyData data = playerData[player.Index];
            if (data == null)
            {
                return;
            }

            // Was the crosshair on a far-enough living enemy at fire time? If so, it's an "attempt".
            if (!IsAimAttempt(player, out _))
            {
                return;
            }

            bool hit = data.pendingHit;
            data.pendingHit = false;

            double now = Server.CurrentTime;
            data.events.Enqueue((now, hit));
            Prune(data, now);

            int attempts = data.events.Count;
            if (attempts < config.Config.MinimumAttempts)
            {
                return;
            }

            int hits = 0;
            foreach (var e in data.events)
            {
                if (e.hit) hits++;
            }

            if (hits * 100 >= attempts * config.Config.RequiredAccuracyPercent)
            {
                double acc = attempts > 0 ? hits * 100.0 / attempts : 0.0;
                Globals.Log($"{player.Controller.PlayerName}: inhuman accuracy -> {hits}/{attempts} ({acc:0.0}%) on-target shots");
                OnDetected(player, data);
            }
        }

        private void OnDetected(PlayerData player, InhumanAccuracyData data)
        {
            int max = config.Config.MaxDetectionsBeforeAction;
            data.detections++;
            data.events.Clear();

            if (data.detections < max)
            {
                return;
            }

            OnPlayerDetected(player, "InhumanAccuracy");
            data.detections = 0;
        }

        private void Prune(InhumanAccuracyData data, double now)
        {
            double cutoff = now - config.Config.WindowSeconds;
            while (data.events.Count > 0 && data.events.Peek().time < cutoff)
            {
                data.events.Dequeue();
            }
        }

        /// <summary>
        /// True if, at this moment, the player's crosshair is within OnTargetAngle of a living enemy
        /// that is at least MinimumDistance away. Outputs the matched angle.
        /// </summary>
        private bool IsAimAttempt(PlayerData player, out float bestAngle)
        {
            bestAngle = 180f;
            try
            {
                var fromOrigin = player.Pawn.AbsOrigin;
                if (fromOrigin == null) return false;

                var from = new Vector3(fromOrigin.X, fromOrigin.Y, fromOrigin.Z + 64f);
                var aim = ForwardFromAngles(player.Pawn.EyeAngles);
                int myTeam = player.Controller.TeamNum;

                foreach (var other in Globals.Players)
                {
                    if (other == null || other.Index == player.Index) continue;
                    if (other.Controller == null || other.Controller.IsValid == false) continue;
                    if (other.Controller.TeamNum == myTeam) continue;        // same team
                    if (other.Controller.TeamNum <= 1) continue;             // spectator/none
                    if (other.Pawn == null || other.Pawn.IsValid == false) continue;
                    if (other.Pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) continue;

                    var tOrigin = other.Pawn.AbsOrigin;
                    if (tOrigin == null) continue;

                    var to = new Vector3(tOrigin.X, tOrigin.Y, tOrigin.Z + 54f);
                    var dir = to - from;
                    float dist = dir.Length();
                    if (dist < config.Config.MinimumDistance) continue;

                    dir = Vector3.Normalize(dir);
                    float dot = Math.Clamp(Vector3.Dot(aim, dir), -1f, 1f);
                    float angle = (float)(Math.Acos(dot) * (180.0 / Math.PI));
                    if (angle < bestAngle) bestAngle = angle;
                }
            }
            catch
            {
                return false;
            }

            return bestAngle <= config.Config.OnTargetAngle;
        }

        private static Vector3 ForwardFromAngles(QAngle angles)
        {
            float pitch = angles.X * (MathF.PI / 180f);
            float yaw = angles.Y * (MathF.PI / 180f);
            float cp = MathF.Cos(pitch);
            return new Vector3(cp * MathF.Cos(yaw), cp * MathF.Sin(yaw), -MathF.Sin(pitch));
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
