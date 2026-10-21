using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CS2AntiCheat.AntiDLLSdk;
using CS2AntiCheat.Core;
using CS2AntiCheat.Handlers;

namespace CS2AntiCheat.Detections.Modules
{
    public class AntiDLLSaveData
    {
        public bool DetectionEnabled { get; set; } = true;

        // Listening to a blacklisted game event is an unambiguous cheat signal, so this defaults to Ban.
        public ActionType DetectionAction { get; set; } = ActionType.Ban;

        // The blacklist of game-event names a cheat DLL registers as a listener (legit clients never
        // do). Edit this list (Configs/AntiDLL.json) to add/remove events - no recompile needed.
        // Shipped with the full default list from the standalone AntiDLL plugin (KillStr3aK).
        public HashSet<string> Blacklist { get; set; } = DefaultBlacklist();

        private static HashSet<string> DefaultBlacklist() =>
        [
            "gameui_hidden", "player_chat", "player_score", "player_shoot", "game_init", "game_start",
            "game_end", "warmup_end", "ugc_map_info_received", "ugc_map_unsubscribed",
            "ugc_map_download_error", "ugc_file_download_finished", "ugc_file_download_start",
            "dm_bonus_weapon_start", "survival_announce_phase", "break_prop", "player_decal",
            "entity_visible", "instructor_server_hint_create", "instructor_server_hint_stop",
            "reset_game_titledata", "vote_ended", "vote_started", "vote_options",
            "endmatch_mapvote_selecting_map", "endmatch_cmm_start_reveal_items", "inventory_updated",
            "client_loadout_changed", "add_player_sonar_icon", "door_open", "door_closed", "door_break",
            "other_death", "bullet_damage", "item_purchase", "bomb_beginplant", "bomb_abortplant",
            "bomb_begindefuse", "bomb_abortdefuse", "hostage_stops_following", "hostage_rescued_all",
            "hostage_call_for_help", "vip_escaped", "vip_killed", "player_radio", "bomb_beep",
            "weapon_fire_on_empty", "weapon_zoom", "silencer_detach", "inspect_weapon",
            "weapon_zoom_rifle", "player_spawned", "item_pickup_slerp", "item_pickup_failed",
            "item_remove", "item_equip", "enter_buyzone", "exit_buyzone", "buytime_ended",
            "enter_bombzone", "exit_bombzone", "enter_rescue_zone", "exit_rescue_zone", "silencer_off",
            "silencer_on", "buymenu_open", "buymenu_close", "round_prestart", "round_poststart",
            "grenade_bounce", "molotov_detonate", "tagrenade_detonate", "inferno_extinguish",
            "decoy_firing", "bullet_impact", "player_footstep", "player_jump", "player_blind",
            "player_falldamage", "door_moving", "mb_input_lock_success", "mb_input_lock_cancel",
            "nav_blocked", "nav_generate", "achievement_info_loaded", "hltv_changed_mode",
            "show_deathpanel", "hide_deathpanel", "player_avenged_teammate", "achievement_earned_local",
            "repost_xbox_achievements", "match_end_conditions", "write_profile_data",
            "trial_time_expired", "update_matchmaking_stats", "enable_restart_voting", "sfuievent",
            "start_vote", "teamchange_pending", "material_default_complete", "cs_prev_next_spectator",
            "tournament_reward", "start_halftime", "ammo_refill", "parachute_pickup",
            "parachute_deploy", "dronegun_attack", "drone_dispatched", "loot_crate_visible",
            "loot_crate_opened", "open_crate_instr", "smoke_beacon_paradrop", "drone_cargo_detached",
            "drone_above_roof", "dz_item_interaction", "survival_teammate_respawn",
            "guardian_wave_restart", "bullet_flight_resolution", "server_shutdown", "server_message",
            "player_full_update", "local_player_team", "local_player_controller_team",
            "local_player_pawn_changed", "ragdoll_dissolved", "team_info", "team_score",
            "hltv_rank_camera", "hltv_rank_entity", "demo_stop", "map_shutdown", "map_transition",
            "hostname_changed", "game_message", "round_start_pre_entity", "round_start_post_nav",
            "teamplay_round_start", "player_hintmessage", "break_breakable", "broken_breakable",
            "door_close", "vote_failed", "vote_passed", "vote_cast_yes", "vote_cast_no",
            "achievement_event", "achievement_write_failed", "bonus_updated", "gameinstructor_draw",
            "gameinstructor_nodraw", "flare_ignite_npc", "helicopter_grenade_punt_miss",
            "physgun_pickup", "cart_updated", "store_pricesheet_updated", "item_schema_initialized",
            "drop_rate_modified", "event_ticket_modified", "gc_connected", "instructor_start_lesson",
            "instructor_close_lesson", "set_instructor_group_enabled", "clientside_lesson_closed",
            "dynamic_shadow_light_changed"
        ];
    }

    /*
     * Module: AntiDLL (self-contained port of KillStr3aK/CS2-AntiDLL)
     * Purpose: Detect cheat DLLs by the game-event listeners they register. Cheats subscribe to
     *          engine events that legitimate clients never listen to; when a client registers a
     *          listener for any event in the Blacklist, that's the detection.
     *
     * HOW: hooks two native functions via CounterStrikeSharp dynamic functions -
     *        CGameEventManager_Init                                 (grab the event manager)
     *        CSource1LegacyGameEventGameSystem::ListenBitsReceived  (a client sent its listen bits)
     *      When the manager is captured, each blacklisted event NAME is resolved to its listen-bit
     *      index (CGameEventManager::GetEventDescriptor) and OR-ed into a 512-bit blacklist mask.
     *      At runtime we read the client's listen-bits straight from the CLCMsg_ListenEvents message
     *      (CUtlVector<uint32> at CLCMsg_ListenEvents::EventArray) and AND them with the mask - any
     *      set bit is a blacklisted event the client subscribed to. No proxy-listener walk, no
     *      per-event FindListener call.
     *
     * The native signatures/offsets live in gamedata/CS2-AntiCheat.AntiDLL.json (editable, no recompile).
     * The event blacklist lives in Configs/AntiDLL.json (editable, no recompile).
     */
    internal class AntiDLLModule : BaseModule
    {
        internal override string Name => "AntiDLL";
        internal override ActionType ActionType => config.Config.DetectionAction;

        private readonly BaseConfig<AntiDLLSaveData> config;
        private IGameEventManager2? gameEventManager;
        private bool hooked;

        // Precomputed blacklist as listen-bit words (16 x uint32 = 512 bits). Built once from the
        // event names when the event manager is captured: for each blacklisted name we resolve its
        // bit index and set that bit. At runtime we just AND the client's listen-bits with this mask.
        private readonly uint[] blacklistMask = new uint[16];
        private bool maskBuilt;

        // Maps a bit index back to the event name(s) it was built from, for readable detection logs.
        private readonly Dictionary<int, string> bitToName = new();

        internal AntiDLLModule() : base()
        {
            config = new BaseConfig<AntiDLLSaveData>("AntiDLL");

            CommandHandler.RegisterCommand("tbac_antidll_action", "Which action to take on the player. 0 = none | 1 = log | 2 = kick | 3 = ban", OnActionCommand);

            TryHook();

            Globals.Log($"AntiDLL Initialized (blacklist: {config.Config.Blacklist.Count} events)");
        }

        /// <summary>
        /// Installs the native hooks. Wrapped in try/catch: if the gamedata signatures are missing or
        /// stale for the current CS2 build, the module stays idle instead of crashing the plugin.
        /// </summary>
        private void TryHook()
        {
            if (hooked == true || config.Config.DetectionEnabled == false)
            {
                return;
            }

            // Bit-parsing path is verified for both Windows (server.dll) and Linux (libserver.so):
            // the message layout (CLCMsg_ListenEvents::EventCount/EventData) and the name->bit resolver
            // (CGameEventManager::GetEventDescriptor) have per-platform signatures/offsets in gamedata.
            try
            {
                IGameEventManager2.Init.Hook(OnGameEventManagerInit, HookMode.Pre);
                CSource1LegacyGameEventGameSystem.ListenBitsReceived.Hook(OnListenBitsReceived, HookMode.Pre);
                hooked = true;
            }
            catch (Exception e)
            {
                Globals.Log($"AntiDLL could not install hooks (bad/missing gamedata signatures?): {e.Message}");
            }
        }

        private HookResult OnGameEventManagerInit(DynamicHook hook)
        {
            gameEventManager = hook.GetParam<IGameEventManager2>(0);
            BuildBlacklistMask();
            return HookResult.Continue;
        }

        /// <summary>
        /// Translates every blacklisted event name into its listen-bit index (via the event manager)
        /// and sets that bit in blacklistMask. Runs once, right after the manager is available. If an
        /// event name is unknown to this build, it is skipped (logged), not fatal.
        /// </summary>
        private void BuildBlacklistMask()
        {
            if (maskBuilt == true || gameEventManager == null)
            {
                return;
            }

            Array.Clear(blacklistMask);
            bitToName.Clear();

            int resolved = 0;
            foreach (var name in config.Config.Blacklist)
            {
                int bit = gameEventManager.GetEventBitIndex(name);
                if (bit < 0 || bit >= 512)
                {
                    continue; // unknown event on this build - skip
                }

                blacklistMask[bit >> 5] |= 1u << (bit & 31);
                bitToName[bit] = name;
                resolved++;
            }

            maskBuilt = true;
            Globals.LogInfo($"AntiDLL blacklist mask built ({resolved}/{config.Config.Blacklist.Count} events resolved to bits)");
        }

        private HookResult OnListenBitsReceived(DynamicHook hook)
        {
            if (config.Config.DetectionEnabled == false)
            {
                return HookResult.Continue;
            }

            // Make sure the mask is ready (manager may have been captured but mask not yet built).
            if (maskBuilt == false)
            {
                BuildBlacklistMask();
                if (maskBuilt == false)
                {
                    return HookResult.Continue;
                }
            }

            CLCMsg_ListenEvents msg = hook.GetParam<CLCMsg_ListenEvents>(1);
            CPlayerSlot slot = msg.GetPlayerSlot();

            CCSPlayerController? controller = Utilities.GetPlayerFromSlot(slot);
            if (controller == null || controller.IsValid == false || controller.IsBot == true)
            {
                return HookResult.Continue;
            }

            // Read the client's listen-bits straight from the message and AND against the blacklist.
            uint[] bits = msg.GetEventBits();
            if (bits.Length == 0)
            {
                return HookResult.Continue;
            }

            var hits = new List<string>();
            int words = Math.Min(bits.Length, blacklistMask.Length);
            for (int w = 0; w < words; w++)
            {
                uint matched = bits[w] & blacklistMask[w];
                while (matched != 0)
                {
                    int bitInWord = System.Numerics.BitOperations.TrailingZeroCount(matched);
                    matched &= matched - 1; // clear lowest set bit
                    int globalBit = (w << 5) + bitInWord;
                    hits.Add(bitToName.TryGetValue(globalBit, out var n) ? n : $"bit#{globalBit}");
                }
            }

            if (hits.Count == 0)
            {
                return HookResult.Continue;
            }

            int index = controller.Slot;
            if (index < 0 || index >= Globals.Players.Length)
            {
                return HookResult.Continue;
            }

            PlayerData? player = Globals.Players[index];
            if (player == null)
            {
                return HookResult.Continue;
            }

            OnPlayerDetected(player, $"AntiDLL -> blacklisted event listener(s): {string.Join(", ", hits)}");
            return HookResult.Continue;
        }

        internal override void OnUnload()
        {
            Shutdown();
        }

        internal void Shutdown()
        {
            if (hooked == false)
            {
                return;
            }

            try
            {
                CSource1LegacyGameEventGameSystem.ListenBitsReceived.Unhook(OnListenBitsReceived, HookMode.Pre);
                IGameEventManager2.Init.Unhook(OnGameEventManagerInit, HookMode.Pre);
            }
            catch { /* ignore */ }

            hooked = false;
        }

        // ----- Commands ----- \\

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
