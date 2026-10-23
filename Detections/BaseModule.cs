using CounterStrikeSharp.API.Core;
using CS2AntiCheat.Core;
using System.Collections.Generic;

namespace CS2AntiCheat.Detections
{
    public enum ActionType : byte
    {
        None = 0,
        Log = 1,
        Kick = 2,
        Ban = 3
    }

    internal abstract class BaseModule
    {
        private readonly HashSet<string> _detectedPlayers = new();

        internal BaseModule() { }

        internal abstract string Name { get; }
        internal abstract ActionType ActionType { get; }

        protected void OnPlayerDetected(PlayerData player, string reason)
        {
            if (!_detectedPlayers.Add(player.SteamID))
            {
                return;
            }

            DetectionMetadata metadata = new()
            {
                module = this,
                player = player,
                time = DateTime.Now,
                reason = reason
            };

            DetectionHandler.OnPlayerDetected(metadata);
        }

        internal virtual void OnPlayerJoin(PlayerData player)
        {
            _detectedPlayers.Remove(player.SteamID);
        }

        internal virtual void OnPlayerLeave(PlayerData player)
        {
            _detectedPlayers.Remove(player.SteamID);
        }

        internal virtual void OnPlayerJump(PlayerData player) { }
        internal virtual void OnPlayerHurt(PlayerData victim, PlayerData shooter, HitGroup_t hitgroup) { }
        internal virtual void OnPlayerDead(PlayerData victim, PlayerData shooter) { }
        internal virtual void OnPlayerShoot(PlayerData player) { }
        internal virtual void OnPlayerTick(PlayerData player) { }

        internal virtual void OnRoundStart() { }
        internal virtual void OnRoundEnd() { }

        internal virtual void OnGameTick() { }

        internal virtual void OnUnload()
        {
            _detectedPlayers.Clear();
        }
    }
}