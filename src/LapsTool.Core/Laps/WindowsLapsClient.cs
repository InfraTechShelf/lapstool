using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using LapsTool.Core.Configuration;

namespace LapsTool.Core.Laps
{
    /// <summary>
    /// Windows LAPS の操作。Microsoft 公式の LAPS PowerShell モジュール
    /// （Get-LapsADPassword / Set-LapsADPasswordExpirationTime）を呼ぶ。
    ///
    /// 暗号化パスワードの復号・権限判定はモジュールと AD に任せ、本ツールでは独自実装しない。
    /// -AsPlainText は付けないので、パスワードは SecureString のまま受け取る。
    /// </summary>
    public sealed class WindowsLapsClient
    {
        private const string ModuleName = "LAPS";

        private readonly AppSettings _settings;

        public WindowsLapsClient(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _settings = settings;
        }

        public LapsPassword GetPassword(LapsComputer computer)
        {
            var results = Invoke("Get-LapsADPassword", computer, null);
            var info = results.FirstOrDefault();
            if (info == null)
                throw new LapsException(computer.Name + " の Windows LAPS パスワードが見つかりません。");

            var password = GetProperty(info, "Password") as SecureString;
            if (password == null)
            {
                // 復号権限が無い場合、オブジェクト自体は返るが Password が空になる。
                var status = Convert.ToString(GetProperty(info, "DecryptionStatus"));
                throw new LapsException(string.IsNullOrEmpty(status)
                    ? "パスワードを参照する権限がありません。"
                    : "パスワードを参照できませんでした（DecryptionStatus: " + status + "）。" +
                      "暗号化パスワードの復号を許可されたグループに所属しているか確認してください。");
            }

            return new LapsPassword(
                computer.Name,
                LapsKind.WindowsLaps,
                Convert.ToString(GetProperty(info, "Account")),
                password,
                ToLocal(GetProperty(info, "PasswordUpdateTime")),
                ToLocal(GetProperty(info, "ExpirationTimestamp")),
                Convert.ToString(GetProperty(info, "Source")));
        }

        public void SetExpiration(LapsComputer computer, DateTime whenEffective)
        {
            Invoke("Set-LapsADPasswordExpirationTime", computer, whenEffective);
        }

        private Collection<PSObject> Invoke(string command, LapsComputer computer, DateTime? whenEffective)
        {
            var iss = InitialSessionState.CreateDefault2();
            iss.ImportPSModule(new[] { ModuleName });

            using (var runspace = RunspaceFactory.CreateRunspace(iss))
            {
                runspace.Open();
                using (var ps = PowerShell.Create())
                {
                    ps.Runspace = runspace;
                    ps.AddCommand(command).AddParameter("Identity", computer.DistinguishedName);
                    if (!string.IsNullOrEmpty(_settings.DomainController))
                        ps.AddParameter("DomainController", _settings.DomainController);
                    if (whenEffective.HasValue)
                        ps.AddParameter("WhenEffective", whenEffective.Value);

                    Collection<PSObject> results;
                    try
                    {
                        results = ps.Invoke();
                    }
                    catch (CommandNotFoundException ex)
                    {
                        throw new LapsException(
                            "Windows LAPS の PowerShell モジュールが見つかりません。" +
                            "Windows 11 / Windows 10（2023年4月以降の更新プログラム適用済み）または " +
                            "Windows Server 2019 以降で実行してください。", ex);
                    }
                    catch (RuntimeException ex)
                    {
                        throw new LapsException(command + " の実行に失敗しました: " + ex.Message, ex);
                    }

                    if (ps.HadErrors && ps.Streams.Error.Count > 0)
                    {
                        var error = ps.Streams.Error[0];
                        throw new LapsException(command + " の実行に失敗しました: " + error.Exception.Message,
                            error.Exception);
                    }

                    return results;
                }
            }
        }

        private static object GetProperty(PSObject obj, string name)
        {
            var prop = obj.Properties[name];
            if (prop == null) return null;
            var value = prop.Value;
            var wrapped = value as PSObject;
            return wrapped != null ? wrapped.BaseObject : value;
        }

        private static DateTime? ToLocal(object value)
        {
            if (!(value is DateTime)) return null;
            var dt = (DateTime)value;
            if (dt == DateTime.MinValue || dt == DateTime.MaxValue) return null;
            return dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
        }
    }
}
