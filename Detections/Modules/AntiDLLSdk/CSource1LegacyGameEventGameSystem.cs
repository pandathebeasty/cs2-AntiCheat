namespace CS2AntiCheat.AntiDLLSdk
{
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

    /// <summary>
    /// The game system that receives a client's "listen bits" message. We only hook
    /// ListenBitsReceived to intercept the CLCMsg_ListenEvents; detection reads the event-bit vector
    /// straight from that message and matches it against a precomputed blacklist mask, so the old
    /// Listeners offset / proxy-listener walk is no longer needed.
    /// </summary>
    public class CSource1LegacyGameEventGameSystem : NativeObject
    {
        public static readonly MemoryFunctionWithReturn<CSource1LegacyGameEventGameSystem, CLCMsg_ListenEvents, bool> ListenBitsReceived = new MemoryFunctionWithReturn<CSource1LegacyGameEventGameSystem, CLCMsg_ListenEvents, bool>(GameData.GetSignature("CSource1LegacyGameEventGameSystem::ListenBitsReceived"));

        public CSource1LegacyGameEventGameSystem(nint ptr) : base(ptr)
            { }
    }
}
