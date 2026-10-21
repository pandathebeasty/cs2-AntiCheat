namespace CS2AntiCheat.AntiDLLSdk
{
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;

    using System.Runtime.InteropServices;

    /// <summary>
    /// Wrapper over the parsed CNetMessagePB&lt;CMsgSource1LegacyListenEvents&gt; a client sends to say
    /// which game events it wants to listen to.
    ///
    /// Layout (verified from CSource1LegacyGameEventGameSystem::ListenBitsReceived on both platforms):
    ///   [msg + 0x58]  player slot (int)
    ///   [msg + 0x48]  event-bit element count (int)
    ///   [msg + 0x50]  pointer to the uint32 word array holding the listen bits (up to 16 words = 512 bits)
    ///                 Bit N set == the client listens to the event with descriptor index N.
    /// On Windows these two offsets are the count+data members of a CUtlVector&lt;uint32&gt; that starts at
    /// 0x48 (count at 0x48, data ptr at 0x50). On Linux they are separate fields at the same offsets.
    /// Both offsets come from gamedata so they can be updated without recompiling.
    /// </summary>
    public class CLCMsg_ListenEvents : NativeObject
    {
        private static readonly int EventCountOffset = GameData.GetOffset("CLCMsg_ListenEvents::EventCount");
        private static readonly int EventDataOffset = GameData.GetOffset("CLCMsg_ListenEvents::EventData");

        private unsafe CPlayerSlot m_Slot => Marshal.PtrToStructure<CPlayerSlot>(base.Handle + 80);

        public CLCMsg_ListenEvents(nint ptr) : base(ptr)
            { }

        public CPlayerSlot GetPlayerSlot() => this.m_Slot;

        /// <summary>
        /// Reads the listen-bits vector from the message and returns it as up to 16 uint32 words
        /// (512 bits). A missing/empty vector returns an empty array. Never throws on bad data.
        /// </summary>
        public uint[] GetEventBits()
        {
            try
            {
                int count = Marshal.ReadInt32(base.Handle + EventCountOffset);
                if (count <= 0 || count > 16)
                {
                    return [];
                }

                nint data = (nint)Marshal.ReadInt64(base.Handle + EventDataOffset);
                if (data == nint.Zero)
                {
                    return [];
                }

                var words = new uint[count];
                for (int i = 0; i < count; i++)
                {
                    words[i] = unchecked((uint)Marshal.ReadInt32(data + (i * 4)));
                }
                return words;
            }
            catch
            {
                return [];
            }
        }
    }
}
