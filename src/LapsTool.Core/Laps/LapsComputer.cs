using System;

namespace LapsTool.Core.Laps
{
    /// <summary>検索結果の1台。パスワードは含まない（取得は明示操作で別に行う）。</summary>
    public sealed class LapsComputer
    {
        public LapsComputer(string name, string dnsHostName, string distinguishedName,
            string operatingSystem, LapsKind kind, DateTime? expirationTime)
        {
            Name = name ?? "";
            DnsHostName = dnsHostName ?? "";
            DistinguishedName = distinguishedName ?? "";
            OperatingSystem = operatingSystem ?? "";
            Kind = kind;
            ExpirationTime = expirationTime;
        }

        /// <summary>コンピューター名（cn）。ローカルアカウントの「ドメイン部」にも使う。</summary>
        public string Name { get; }

        public string DnsHostName { get; }

        public string DistinguishedName { get; }

        public string OperatingSystem { get; }

        public LapsKind Kind { get; }

        /// <summary>パスワード有効期限（ローカル時刻）。</summary>
        public DateTime? ExpirationTime { get; }

        public string KindDisplay
        {
            get { return LapsKindText.ToDisplay(Kind); }
        }

        public LapsComputer WithExpiration(DateTime? expirationTime)
        {
            return new LapsComputer(Name, DnsHostName, DistinguishedName, OperatingSystem, Kind, expirationTime);
        }
    }
}
