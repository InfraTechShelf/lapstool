namespace LapsTool.Core.Laps
{
    /// <summary>コンピューターを管理している LAPS の種類。</summary>
    public enum LapsKind
    {
        /// <summary>LAPS 管理の痕跡（有効期限属性）が見つからない。</summary>
        None,

        /// <summary>Windows LAPS（msLAPS-* 属性。暗号化・平文の両方を含む）。</summary>
        WindowsLaps,

        /// <summary>旧 Microsoft LAPS（ms-Mcs-AdmPwd 属性）。</summary>
        LegacyLaps,
    }

    public static class LapsKindText
    {
        public static string ToDisplay(LapsKind kind)
        {
            switch (kind)
            {
                case LapsKind.WindowsLaps: return "Windows LAPS";
                case LapsKind.LegacyLaps: return "旧LAPS";
                default: return "未管理";
            }
        }
    }
}
