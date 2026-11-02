using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using LapsTool.Core.Configuration;
using LapsTool.Core.Laps;
using LapsTool.Core.Logging;

namespace LapsTool.Core.Rdp
{
    /// <summary>
    /// 参照したパスワードでリモートデスクトップ接続する。
    ///
    /// 流れ: 資格情報を一時登録 → mstsc /v:&lt;接続先&gt; を起動 → 一定時間後に削除。
    /// 同じ接続先へ続けて接続した場合に、古いタイマーが新しい登録を消さないよう
    /// 接続先ごとに世代番号を持つ。ツール終了時は <see cref="DeleteAllPending"/> で残りを消す。
    /// </summary>
    public sealed class RdpLauncher
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, int> _pending =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly OperationLog _log;

        public RdpLauncher(OperationLog log)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            _log = log;
        }

        /// <summary>接続先アドレス（mstsc /v: に渡す値。資格情報のターゲット名と一致させる必要がある）。</summary>
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
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var address = ResolveAddress(computer, settings);
            var userName = ResolveUserName(computer, password, settings);

            try
            {
                TemporaryRdpCredential.Write(address, userName, password.Password);

                int generation;
                lock (_gate)
                {
                    int current;
                    _pending.TryGetValue(address, out current);
                    generation = current + 1;
                    _pending[address] = generation;
                }

                var args = "/v:" + address + (settings.RdpAdminMode ? " /admin" : "");
                try
                {
                    Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "mstsc.exe"), args)
                    {
                        UseShellExecute = false,
                    });
                }
                catch
                {
                    // 起動できなかったなら登録を残す理由が無い。
                    DeleteIfCurrent(address, generation);
                    throw;
                }

                _log.Write("RDP接続", computer.Name, "mstsc起動: " + address + " / " + userName);
                ScheduleDelete(address, generation, settings.CredentialDeleteSeconds);
            }
            catch (Exception ex)
            {
                _log.Write("RDP接続", computer.Name, "失敗: " + ex.Message);
                throw;
            }
        }

        /// <summary>まだ削除されていない一時資格情報をすべて消す（ツール終了時）。</summary>
        public void DeleteAllPending()
        {
            List<string> targets;
            lock (_gate)
            {
                targets = new List<string>(_pending.Keys);
                _pending.Clear();
            }

            foreach (var address in targets)
            {
                try
                {
                    TemporaryRdpCredential.Delete(address);
                }
                catch (Exception)
                {
                    // 終了処理なので握りつぶす。CRED_PERSIST_SESSION なのでログオフ時に消える。
                }
            }
        }

        private void ScheduleDelete(string address, int generation, int seconds)
        {
            Task.Delay(TimeSpan.FromSeconds(seconds)).ContinueWith(_ =>
            {
                try
                {
                    DeleteIfCurrent(address, generation);
                }
                catch (Exception ex)
                {
                    _log.Write("RDP接続", address, "一時資格情報の削除に失敗: " + ex.Message);
                }
            }, TaskScheduler.Default);
        }

        private void DeleteIfCurrent(string address, int generation)
        {
            lock (_gate)
            {
                int current;
                if (!_pending.TryGetValue(address, out current) || current != generation)
                    return; // 後から同じ接続先へ接続し直している。そちらのタイマーに任せる。
                _pending.Remove(address);
            }
            TemporaryRdpCredential.Delete(address);
        }
    }
}
