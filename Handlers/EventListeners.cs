using CounterStrikeSharp.API.Core;
using CS2AntiCheat.Core;
using CS2AntiCheat.Detections;

namespace CS2AntiCheat.Handlers
{
    public static class EventListeners
    {
        internal static void Initialize(BasePlugin plugin)
        {
            plugin.RegisterListener<Listeners.OnTick>(OnGameTick);
            plugin.RegisterListener<Listeners.OnMapStart>(OnMapStart);
            plugin.RegisterListener<Listeners.OnMapEnd>(OnMapEnd);

            Globals.Log($"EventListeners Initialized");
        }

        private static void OnMapStart(string mapName)
        {
            Globals.Initialize(false);
            Globals.RefreshServerInfo();
        }

        private static void OnMapEnd() { }

        private static void OnGameTick()
        {
            BaseCaller.OnGameTick();

            foreach (PlayerData player in Globals.Players)
            {
                if (player == null)
                {
                    continue;
                }

                BaseCaller.OnPlayerTick(player);
            }
        }
    }
}
