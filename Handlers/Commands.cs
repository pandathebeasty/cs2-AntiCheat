using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2AntiCheat.Core;

namespace CS2AntiCheat.Handlers
{
    internal static class CommandHandler
    {
        private static BasePlugin? plugin;

        /// <summary>
        /// True when the caller may run an admin command. The server console (null) always passes;
        /// an in-game player must hold the configurable permission (General.json CommandPermission,
        /// default @css/root). Replies are left to the caller.
        /// </summary>
        internal static bool HasPermission(CCSPlayerController? player)
        {
            if (player == null)
            {
                return true; // server console
            }

            if (player.IsValid == false)
            {
                return false;
            }

            string flag = GeneralConfig.GetGeneralConfig().Config.CommandPermission;
            if (string.IsNullOrWhiteSpace(flag))
            {
                return true; // no restriction configured
            }

            return AdminManager.PlayerHasPermissions(player, flag);
        }

        internal static void Initialize(BasePlugin basePlugin)
        {
            plugin = basePlugin;

            Globals.Log($"CommandHandler Initialized");
        }

        internal static bool RegisterCommand(string command, string description, CommandInfo.CommandCallback handler)
        {
            if (plugin == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(command) == true)
            {
                return false;
            }

            if (handler == null)
            {
                return false;
            }

            plugin.AddCommand(command, description, handler);
            return true;
        }
    }
}
