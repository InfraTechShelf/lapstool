using System;
using System.Diagnostics;
using System.IO;
using LapsTool.Core.Configuration;
using LapsTool.Core.Laps;
using LapsTool.Core.Logging;
using Microsoft.Win32;

namespace LapsTool.Core.Rdp
{
    /// <summary>
    /// 参照した LAPS のアカウントでリモートデスクトップ接続を始める。
    ///
    /// mstsc にはユーザー名だけを渡し、パスワードは利用者が入力する。
    /// 資格情報マネージャーに保存して自動ログオンする方式は採らない。LAPS のアカウントはローカル
    /// アカウントなので RDP の認証が必ず NTLM になり、Windows の既定のポリシーでは保存資格情報が
    /// 拒否されるため（許可するにはサーバーのなりすましを防げなくなるポリシー変更が要る）。
    /// </summary>
    public sealed class RdpLauncher
    {
        private readonly OperationLog _log;

        public RdpLauncher(OperationLog log)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            _log = log;
        }

        /// <summary>接続先アドレス（mstsc /v: に渡す値）。</summary>
        public static string ResolveAddress(LapsComputer computer, AppSettings settings)
        {
            return settings.ConnectByFqdn && !string.IsNullOrEmpty(computer.DnsHostName)
                ? computer.DnsHostName
                : computer.Name;
        }

        /// <summary>接続に使うアカウント名（ドメイン部を除く）。</summary>
        public static string ResolveAccount(LapsPassword password, AppSettings settings)
        {
            if (settings.PreferLapsAccountName && password != null && !string.IsNullOrEmpty(password.Account))
                return password.Account;
            return settings.LocalAccountName;
        }

        /// <summary>ローカルアカウントであることを明示した「コンピューター名\アカウント名」形式。</summary>
        public static string ResolveUserName(LapsComputer computer, LapsPassword password, AppSettings settings)
        {
            return computer.Name + "\\" + ResolveAccount(password, settings);
        }

        public void Launch(LapsComputer computer, LapsPassword password, AppSettings settings)
        {
            if (computer == null) throw new ArgumentNullException(nameof(computer));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var address = ResolveAddress(computer, settings);
            var userName = ResolveUserName(computer, password, settings);

            try
            {
                SetUsernameHint(address, userName);

                var args = "/v:" + address + (settings.RdpAdminMode ? " /admin" : "");
                Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "mstsc.exe"), args)
                {
                    UseShellExecute = false,
                });

                _log.Write("RDP接続", computer.Name, "mstsc起動: " + address + " / " + userName);
            }
            catch (Exception ex)
            {
                _log.Write("RDP接続", computer.Name, "失敗: " + ex.Message);
                throw;
            }
        }

        /// <summary>
        /// mstsc が資格情報の入力画面に出すユーザー名（接続先ごとの UsernameHint）を、LAPS のアカウントにする。
        /// 放っておくと、前回その接続先に使った別のアカウント（例: ドメインの Administrator）が出る。
        /// mstsc 自身も接続成功時に同じ値を書くので、上書きしても副作用は無い。
        /// </summary>
        private static void SetUsernameHint(string address, string userName)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Terminal Server Client\Servers\" + address))
                {
                    if (key != null) key.SetValue("UsernameHint", userName, RegistryValueKind.String);
                }
            }
            catch (Exception)
            {
                // 入力欄の初期値が変わらないだけなので、接続自体は止めない。
            }
        }
    }
}
