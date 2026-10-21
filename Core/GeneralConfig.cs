using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Core
{
    public class GeneralConfigData
    {
        // Master on/off switch for the whole anti-cheat. When false, no detection runs (player
        // tracking still works, so toggling with tbac_reload takes effect immediately).
        public bool Enabled { get; set; } = true;

        // When true, the plugin prints all console log lines (init messages, detection logs, debug
        // info). When false, the console stays silent - Discord alerts still work independently.
        public bool Debug { get; set; } = false;

        // Prefix prepended to every console log line (e.g. "[CS2AC] Loaded ..."). Change it to
        // whatever tag you want to grep for in the server console. A trailing space is added
        // automatically, so leave it as just the tag. Leave empty for no prefix.
        public string LogPrefix { get; set; } = "[CS2AC]";

        // Permission flag required to run the plugin's admin commands (tbac_reload, tbac_*_action,
        // tbac_aimbot_angle, ...). The server console always bypasses this.
        public string CommandPermission { get; set; } = "@css/root";

        // Commands run (on the server console) when a module's action is Ban. They run in order:
        // BanCommand first, then KickCommand. Both are optional - leave empty to skip that step.
        // Placeholders: {id} = userid (use #{id} to target by userid), {steamid} = SteamID64,
        //               {steamid2} = STEAM_0:..., {name} = player name, {reason} = detection reason,
        //               {module} = detection module name.
        public string BanCommand { get; set; } = "css_ban #{id} 0 \"ANTICHEAT: {reason}\"";
        public string KickCommand { get; set; } = "css_kick #{id}";

        // Global switch for Discord detection alerts. When false, no detection sends to Discord
        // regardless of module. (A webhook URL is still required for alerts to work when true.)
        public bool DiscordAlerts { get; set; } = true;

        // Full Discord webhook URL, e.g. https://discord.com/api/webhooks/<id>/<token>
        // Leave empty to disable Discord notifications.
        public string DiscordWebhookUrl { get; set; } = string.Empty;

        // Who to ping on a detection message. Options:
        //   ""           = no ping
        //   "everyone"   = pings @everyone
        //   "here"       = pings @here
        //   "<role id>"  = pings that role (numeric Discord role id, e.g. "123456789012345678")
        public string DiscordMentionRoleId { get; set; } = string.Empty;
    }

    internal static class GeneralConfig
    {
        private static BaseConfig<GeneralConfigData> config = null!;

        public static void Initialize()
        {
            LoadConfig();

            CommandHandler.RegisterCommand("tbac_reload", "Reloads the config", OnReloadCommand);
        }

        public static BaseConfig<GeneralConfigData> GetGeneralConfig()
        {
            return config;
        }

        private static void LoadConfig()
        {
            config = new BaseConfig<GeneralConfigData>("General");

            // Mirror the switches into Globals so the hot paths can read them cheaply.
            Globals.Enabled = config.Config.Enabled;
            Globals.DebugLogging = config.Config.Debug;
            Globals.LogPrefix = config.Config.LogPrefix;

            Globals.Log($"Loaded general config (anti-cheat {(Globals.Enabled ? "ENABLED" : "DISABLED")})");
        }

        // ----- Commands ----- \\

        private static void OnReloadCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (CommandHandler.HasPermission(player) == false)
            {
                return;
            }

            LoadConfig();
        }
    }
}
