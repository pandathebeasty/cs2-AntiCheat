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
        internal static void OnPlayerDetected(DetectionMetadata metadata)
        {
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

            // Discord notification, gated by the global DiscordAlerts switch (General.json).
            if (GeneralConfig.GetGeneralConfig().Config.DiscordAlerts == true)
            {
                NotifyDiscord(metadata, punishment);
            }
        }

        /// <summary>
        /// Runs the configurable ban + kick commands (in that order) on the server console. Both
        /// come from General.json and support placeholders; an empty template skips that step.
        /// </summary>
        private static void BanPlayerWithCommands(DetectionMetadata metadata)
        {
            var cfg = GeneralConfig.GetGeneralConfig().Config;
            var banCommand = BuildCommand(cfg.BanCommand, metadata);
            var kickCommand = BuildCommand(cfg.KickCommand, metadata);

            // Execute on the main thread/next frame (detections can fire off the main thread).
            Server.NextFrame(() =>
            {
                if (string.IsNullOrWhiteSpace(banCommand) == false)
                {
                    Globals.Log($"Executing ban command: {banCommand}");
                    Server.ExecuteCommand(banCommand);
                }

                if (string.IsNullOrWhiteSpace(kickCommand) == false)
                {
                    Globals.Log($"Executing kick command: {kickCommand}");
                    Server.ExecuteCommand(kickCommand);
                }
            });
        }

        /// <summary>Fills the configurable command template with this detection's player data.</summary>
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
