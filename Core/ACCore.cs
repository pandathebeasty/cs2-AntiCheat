using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core;
using CS2AntiCheat.Handlers;
using CS2AntiCheat.Integration;
using Microsoft.Extensions.Logging;

namespace CS2AntiCheat.Core
{
    [MinimumApiVersion(374)]
    public class ACCore : BasePlugin
    {
        public override string ModuleName => "CS2-AntiCheat";
        public override string ModuleVersion => "1.0.1";
        public override string ModuleAuthor => "panda";
        public override string ModuleDescription => "Anti-Cheat for CS2";

        public override void Unload(bool hotReload)
        {
            foreach (var module in Globals.Modules)
            {
                try { module.OnUnload(); }
                catch (Exception e) { Globals.Log($"Exception in {module.Name}.OnUnload -> {e.Message}"); }
            }
        }

        public override void Load(bool hotReload)
        {
            // Always-on load logging (goes to the CounterStrikeSharp log file in addons/.../logs/,
            // independent of the General.json "Debug" switch which only gates verbose runtime logs).
            Logger.LogInformation("Loading {Name} v{Version} (hotReload: {HotReload})", ModuleName, ModuleVersion, hotReload);

            Globals.PreInit(this, Logger);
            Globals.Log($"Loading (hotReload: {hotReload})");

            if (hotReload == true)
            {
                Globals.Initialize(hotReload);
            }

            CommandHandler.Initialize(this);

            EventListeners.Initialize(this);
            EventHandlers.Initialize(this, hotReload);

            UserMessagesHandler.Initialize(this);

            GeneralConfig.Initialize();
            Globals.RefreshServerInfo();
            DiscordIntegration.InitializeWebhook();

            // Always-on: written to the CSSharp log file regardless of the Debug switch. The per-module
            // summary is logged from Globals.Initialize (which runs on map start / hot reload, once the
            // modules actually exist).
            Logger.LogInformation("Loaded {Name} v{Version}", ModuleName, ModuleVersion);
        }
    }
}
