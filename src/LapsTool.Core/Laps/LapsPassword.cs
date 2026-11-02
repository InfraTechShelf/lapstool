using System;
using System.Security;

namespace LapsTool.Core.Laps
{
    /// <summary>
    /// 取得したパスワード情報。パスワードは <see cref="SecureString"/> で保持し、
    /// 表示・接続など必要な時点でだけ平文化する。
    /// </summary>
    public sealed class LapsPassword : IDisposable
    {
        public LapsPassword(string computerName, LapsKind kind, string account, SecureString password,
            DateTime? passwordUpdateTime, DateTime? expirationTime, string source)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            ComputerName = computerName ?? "";
            Kind = kind;
            Account = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
            Password = password;
            Password.MakeReadOnly();
            PasswordUpdateTime = passwordUpdateTime;
            ExpirationTime = expirationTime;
            Source = source ?? "";
        }

        public string ComputerName { get; }

        public LapsKind Kind { get; }

        /// <summary>LAPS が記録しているアカウント名。旧 LAPS では常に null。</summary>
        public string Account { get; }

        public SecureString Password { get; }

        public DateTime? PasswordUpdateTime { get; }

        public DateTime? ExpirationTime { get; }

        /// <summary>取得元（Windows LAPS の Source 値など）。表示用。</summary>
        public string Source { get; }

        public void Dispose()
        {
            Password.Dispose();
        }
    }
}
