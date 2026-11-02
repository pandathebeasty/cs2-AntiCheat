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
        //               {steamid2} = STEAM_0:..., {name} = player name,
        //               {module} = detection module name only (e.g. "RapidFire"),
        //               {reason} = full detection detail (e.g. "RapidFire -> weapon_m4a1 ...").
        // The public ban reason uses {module} so players only see "ANTICHEAT: RapidFire"; the full
        // {reason} detail still goes to the console log and Discord. Swap to {reason} if you want the
        // detail in the ban message too.
        public string BanCommand { get; set; } = "css_ban #{id} 0 \"ANTICHEAT: {module}\"";
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

        // --- Accuracy gates (reduce false positives) ------------------------------------------

        // Skip timing/movement detections during warmup and freeze time. In these phases players are
        // frozen, respawning, swapping weapons in buy time, or replaced by bots - all of which can
        // look like a cheat to a timing-based module. Strongly recommended ON.
        public bool SkipDuringWarmupAndFreeze { get; set; } = true;

        // Skip timing-sensitive detections for players whose ping is at/above this value (ms). High
        // latency makes subtick timing and angle deltas unreliable, which is a common false-positive
        // source for rapid fire / impossible yaw / no-recoil. 0 = never skip on ping.
        public int MaxPingForTimingChecks { get; set; } = 150;
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
            Globals.SkipDuringWarmupAndFreeze = config.Config.SkipDuringWarmupAndFreeze;
            Globals.MaxPingForTimingChecks = config.Config.MaxPingForTimingChecks;

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
