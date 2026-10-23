using CounterStrikeSharp.API;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using CS2AntiCheat.Detections;
using CS2AntiCheat.Integration;

namespace CS2AntiCheat.Core
{
    internal struct DetectionMetadata
    {
        internal BaseModule module;
        internal PlayerData player;
        internal DateTime time;
        internal string reason;
    }

    internal static class DetectionHandler
    {
        private static readonly HashSet<string> ProcessedPlayers = new();

        internal static void OnPlayerDetected(DetectionMetadata metadata)
        {
            if (!ProcessedPlayers.Add(metadata.player.SteamID))
            {
                return;
            }
            
            string punishment;

            switch (metadata.module.ActionType)
            {
                case ActionType.None:
                {
                    return;
                }

                case ActionType.Log:
                {
                    Globals.Log($"{metadata.player.Controller.PlayerName} is suspected of using {metadata.module.Name} ({metadata.reason})");
                    punishment = "No punishment (log only).";
                    break;
                }

                case ActionType.Kick:
                {
                    Globals.Log($"{metadata.player.Controller.PlayerName} was kicked for using {metadata.module.Name} ({metadata.reason})");
                    metadata.player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_DISCONNECT_BY_SERVER);
                    punishment = "Player was kicked.";
                    break;
                }

                case ActionType.Ban:
                {
                    Globals.Log($"{metadata.player.Controller.PlayerName} was banned for using {metadata.module.Name} ({metadata.reason})");
                    BanPlayerWithCommands(metadata);
                    punishment = "Player was banned.";
                    break;
                }

                default:
                {
                    return;
                }
            }

            if (GeneralConfig.GetGeneralConfig().Config.DiscordAlerts)
            {
                NotifyDiscord(metadata, punishment);
            }
        }

        internal static void OnPlayerLeave(PlayerData player)
        {
            ProcessedPlayers.Remove(player.SteamID);
        }

        private static void BanPlayerWithCommands(DetectionMetadata metadata)
        {
            var cfg = GeneralConfig.GetGeneralConfig().Config;
            var banCommand = BuildCommand(cfg.BanCommand, metadata);
            var kickCommand = BuildCommand(cfg.KickCommand, metadata);

            Server.NextFrame(() =>
            {
                if (!string.IsNullOrWhiteSpace(banCommand))
                {
                    Globals.Log($"Executing ban command: {banCommand}");
                    Server.ExecuteCommand(banCommand);
                }

                if (!string.IsNullOrWhiteSpace(kickCommand))
                {
                    Globals.Log($"Executing kick command: {kickCommand}");
                    Server.ExecuteCommand(kickCommand);
                }
            });
        }

        private static string BuildCommand(string template, DetectionMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            var controller = metadata.player.Controller;
            var userId = controller.UserId?.ToString() ?? "0";
            var steamId64 = controller.AuthorizedSteamID?.SteamId64.ToString() ?? "0";
            var steamId2 = controller.AuthorizedSteamID?.SteamId2 ?? metadata.player.SteamID;

            return template
                .Replace("{id}", userId)
                .Replace("{steamid}", steamId64)
                .Replace("{steamid2}", steamId2)
                .Replace("{name}", controller.PlayerName)
                .Replace("{reason}", metadata.reason)
                .Replace("{module}", metadata.module.Name);
        }

        private static void NotifyDiscord(DetectionMetadata metadata, string punishment)
        {
            var controller = metadata.player.Controller;
            var steamId64 = controller.AuthorizedSteamID?.SteamId64.ToString() ?? "0";

            var report = new DetectionReport
            {
                PlayerName = controller.PlayerName,
                SteamId64 = steamId64,
                ProfileUrl = $"https://steamcommunity.com/profiles/{steamId64}",
                Detection = metadata.module.Name,
                Evidence = metadata.reason,
                Punishment = punishment,
                Map = Server.MapName,
                Address = Globals.ServerAddress,
                ServerName = Globals.ServerName
            };

            DiscordIntegration.SendDetection(report);
        }
    }
}