namespace CS2AntiCheat.AntiDLLSdk
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Explicit)]
    public struct CUtlString
    {
        [FieldOffset(0x0)]
        public string m_pString;

        public string Get() => this.m_pString;

        public override bool Equals(object? obj)
        {
            if (obj is CUtlString utlString)
            {
                return this == utlString;
            }
            else if (obj is string str)
            {
                return this == str;
            }

            return false;
        }

        public override int GetHashCode() => this.m_pString?.GetHashCode() ?? 0;

        public override string ToString() => this.m_pString;

        public static bool operator ==(CUtlString a, CUtlString b) => a.m_pString == b.m_pString;

        public static bool operator !=(CUtlString a, CUtlString b) => a.m_pString != b.m_pString;

        public static bool operator ==(CUtlString a, string b) => a.m_pString == b;

        public static bool operator !=(CUtlString a, string b) => a.m_pString != b;

        public static implicit operator string(CUtlString str) => str.m_pString;
    }
}
