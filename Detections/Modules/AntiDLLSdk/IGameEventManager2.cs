namespace CS2AntiCheat.AntiDLLSdk
{
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Modules.Memory;
    using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

    using System.Runtime.InteropServices;

    public class IGameEventManager2 : NativeObject
    {
        public static readonly MemoryFunctionVoid<IGameEventManager2> Init = new MemoryFunctionVoid<IGameEventManager2>(GameData.GetSignature("CGameEventManager_Init"));

        // bool GetEventDescriptor(CGameEventManager* this, const char* name, int* outBitIndex)
        // Returns a descriptor pointer (non-null = found) and writes the event's bit index into
        // *outBitIndex (the engine ORs in bit 31 as a flag, so mask with 0x7FFFFFFF).
        private static readonly MemoryFunctionWithReturn<nint, string, nint, nint> GetEventDescriptorFn =
            new MemoryFunctionWithReturn<nint, string, nint, nint>(GameData.GetSignature("CGameEventManager::GetEventDescriptor"));

        public IGameEventManager2(nint ptr) : base(ptr)
            { }

        /// <summary>
        /// Resolves an event name to its listen-bit index. Returns -1 if the event does not exist
        /// (or on any failure). The returned index is the bit position in the client's listen-bits
        /// vector (see CLCMsg_ListenEvents.GetEventBits).
        /// </summary>
        public int GetEventBitIndex(string eventName)
        {
            try
            {
                nint outIndex = Marshal.AllocHGlobal(sizeof(int));
                try
                {
                    Marshal.WriteInt32(outIndex, -1);
                    nint descriptor = GetEventDescriptorFn.Invoke(base.Handle, eventName, outIndex);
                    if (descriptor == nint.Zero)
                    {
                        return -1;
                    }

                    int raw = Marshal.ReadInt32(outIndex);
                    if (raw == -1)
                    {
                        return -1;
                    }

                    return raw & 0x7FFFFFFF; // strip the bit-31 flag the engine sets
                }
                finally
                {
                    Marshal.FreeHGlobal(outIndex);
                }
            }
            catch
            {
                return -1;
            }
        }
    }
}
