using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Microsoft.Extensions.Logging;
using CS2AntiCheat.Detections;
using CS2AntiCheat.Detections.Modules;

namespace CS2AntiCheat.Core
{
    internal class PlayerData
    {
        internal required CCSPlayerController Controller;
        internal required CCSPlayerPawn Pawn;

        internal required int Index;
        internal required bool IsBot;

        internal string PlayerName => Controller.PlayerName;
        internal string SteamID => Controller.AuthorizedSteamID?.SteamId2 ?? "Invalid SteamID";

        internal CCSWeaponBaseGun GetWeapon()
        {
            CPlayer_WeaponServices? weaponServices = Pawn.WeaponServices;
            if (weaponServices == null)
            {
                return null!;
            }

            CBasePlayerWeapon? weaponBase = weaponServices.ActiveWeapon.Value;
            if (weaponBase == null)
            {
                return null!;
            }

            return new CCSWeaponBaseGun(weaponBase.Handle);
        }

        internal void Disconnect(NetworkDisconnectionReason reason)
        {
            Controller.Disconnect(reason);
        }
    }

    internal static class Globals
    {
        private static bool initializedOnce = false;

        private static ACCore? pluginCore = null;
        private static ILogger? logger = null;

        // Master on/off switch, driven by General.json "Enabled". Checked by the detection dispatch
        // so the whole anti-cheat can be turned off from config (reload with tbac_reload).
        internal static bool Enabled = true;

        // Gates all console logging. Driven by General.json "Debug". Off until the config loads, so
        // nothing prints before we know the setting.
        internal static bool DebugLogging = false;

        // Prefix prepended to every console log line. Driven by General.json "LogPrefix". Defaults to
        // "[CS2AC]" so early init lines (before the config loads) are still tagged consistently.
        internal static string LogPrefix = "[CS2AC]";

        // Server info for Discord embeds, cached on the main thread (ConVar reads are main-thread only).
        internal static string ServerName = "CS2 Server";
        internal static string ServerAddress = "unknown";

        /// <summary>Refreshes the cached hostname + ip:port. Call on the main thread (map start/load).</summary>
        internal static void RefreshServerInfo()
        {
            try
            {
                var hostname = ConVar.Find("hostname")?.StringValue;
                if (string.IsNullOrWhiteSpace(hostname) == false)
                {
                    ServerName = hostname;
                }

                var ip = ConVar.Find("ip")?.StringValue;
                var port = ConVar.Find("hostport")?.GetPrimitiveValue<int>();
                if (string.IsNullOrWhiteSpace(ip) == false && ip != "0.0.0.0" && port is > 0)
                {
                    ServerAddress = $"{ip}:{port}";
                }
            }
            catch
            {
                // keep previous values on failure
            }
        }

        internal static PlayerData[] Players = [];
        internal static BaseModule[] Modules = [];

        internal static void PreInit(ACCore core, ILogger log)
        {
            pluginCore = core;
            logger = log;
        }

        internal static void Initialize(bool forceReinitialize)
        {
            Players = new PlayerData[Server.MaxPlayers];
            if (initializedOnce == true && forceReinitialize == false)
            {
                return;
            }

            Log($"Globals Initializing (forced: {forceReinitialize})");

            Modules =
            [
                new Aimbot(),
                new Backtrack(),
                new BunnyHop(),
                new RapidFire(),
                new UntrustedAngles(),
                new ImpossibleYaw(),
                new DuckSpam(),
                new UserCmdRate(),
                new NoRecoil(),
                new AntiDLLModule()
            ];

            initializedOnce = true;

            // Always-on: record which detection modules were loaded (written to the CSSharp log file).
            LogInfo($"Initialized {Modules.Length} detection module(s): {string.Join(", ", Modules.Select(m => m.Name))}");
        }

        internal static string GetModuleDirectory()
        {
            if (pluginCore == null)
            {
                return string.Empty;
            }

            return pluginCore.ModuleDirectory;
        }

        internal static void Log(string message)
        {
            if (DebugLogging == false)
            {
                return;
            }

            var line = string.IsNullOrEmpty(LogPrefix) ? message : $"{LogPrefix} {message}";
            logger?.Log(LogLevel.Information, line);
        }

        /// <summary>
        /// Always-on logging: writes to the CounterStrikeSharp log file (addons/.../logs/) regardless
        /// of the "Debug" switch. Use for load/init milestones that should always be recorded.
        /// </summary>
        internal static void LogInfo(string message)
        {
            var line = string.IsNullOrEmpty(LogPrefix) ? message : $"{LogPrefix} {message}";
            logger?.Log(LogLevel.Information, line);
        }
    }
}
