namespace CS2AntiCheat.AntiDLLSdk
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Explicit)]
    public struct CPlayerSlot
    {
        [FieldOffset(0x0)]
        private int m_Data;

        public int Get() => this.m_Data;

        public static implicit operator int(CPlayerSlot slot) => slot.m_Data;
    }
}
