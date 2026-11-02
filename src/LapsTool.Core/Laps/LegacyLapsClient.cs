using System;
using System.DirectoryServices;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using LapsTool.Core.Configuration;

using AdSearchScope = System.DirectoryServices.SearchScope;

namespace LapsTool.Core.Laps
{
    /// <summary>
    /// 旧 Microsoft LAPS の操作。ms-Mcs-AdmPwd（機密属性）を現在のユーザーの権限で読む。
    /// 読み取り権限が無いと AD は属性を返さないので、「無い」と「権限が無い」を区別できない。
    /// </summary>
    public sealed class LegacyLapsClient
    {
        private const string PasswordAttribute = "ms-Mcs-AdmPwd";

        private readonly AppSettings _settings;
        private readonly ComputerDirectory _directory;

        public LegacyLapsClient(AppSettings settings, ComputerDirectory directory)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (directory == null) throw new ArgumentNullException(nameof(directory));
            _settings = settings;
            _directory = directory;
        }

        public LapsPassword GetPassword(LapsComputer computer)
        {
            try
            {
                using (var entry = new DirectoryEntry(_directory.BuildPath(computer.DistinguishedName)))
                using (var searcher = new DirectorySearcher(entry, "(objectClass=computer)"))
                {
                    searcher.SearchScope = AdSearchScope.Base;
                    searcher.PropertiesToLoad.Add(PasswordAttribute);
                    searcher.PropertiesToLoad.Add(ComputerDirectory.LegacyLapsExpirationAttribute);

                    var r = searcher.FindOne();
                    if (r == null || !r.Properties.Contains(PasswordAttribute))
                    {
                        throw new LapsException(
                            "パスワードを取得できませんでした。参照権限が無いか、まだパスワードが設定されていません。");
                    }

                    var secure = new SecureString();
                    foreach (var c in (string)r.Properties[PasswordAttribute][0])
                        secure.AppendChar(c);

                    return new LapsPassword(
                        computer.Name,
                        LapsKind.LegacyLaps,
                        null,
                        secure,
                        null,
                        ComputerDirectory.ReadFileTime(r, ComputerDirectory.LegacyLapsExpirationAttribute),
                        "ms-Mcs-AdmPwd");
                }
            }
            catch (COMException ex)
            {
                throw new LapsException("旧LAPSパスワードの取得に失敗しました: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// ms-Mcs-AdmPwdExpirationTime を書き換える（AdmPwd.PS の Reset-AdmPwdPassword と同じ操作）。
        /// 実際のパスワード変更は、対象端末で次にグループポリシーが処理されたときに行われる。
        /// </summary>
        public void SetExpiration(LapsComputer computer, DateTime whenEffective)
        {
            var server = string.IsNullOrEmpty(_settings.DomainController) ? null : _settings.DomainController;
            var fileTime = whenEffective.ToUniversalTime().ToFileTimeUtc();

            try
            {
                using (var connection = new LdapConnection(new LdapDirectoryIdentifier(server)))
                {
                    connection.AuthType = AuthType.Negotiate;
                    connection.Credential = CredentialCache.DefaultNetworkCredentials;
                    // LDAP 署名を必須にしている DC でも通るよう、署名・暗号化を有効にする。
                    connection.SessionOptions.Signing = true;
                    connection.SessionOptions.Sealing = true;
                    connection.Bind();

                    var request = new ModifyRequest(
                        computer.DistinguishedName,
                        DirectoryAttributeOperation.Replace,
                        ComputerDirectory.LegacyLapsExpirationAttribute,
                        fileTime.ToString(CultureInfo.InvariantCulture));
                    connection.SendRequest(request);
                }
            }
            catch (DirectoryOperationException ex)
            {
                var code = ex.Response != null ? ex.Response.ResultCode : ResultCode.Other;
                if (code == ResultCode.InsufficientAccessRights)
                    throw new LapsException("有効期限を変更する権限がありません。", ex);
                throw new LapsException("有効期限の変更に失敗しました: " + ex.Message, ex);
            }
            catch (LdapException ex)
            {
                throw new LapsException("ドメインコントローラーに接続できませんでした: " + ex.Message, ex);
            }
        }
    }
}
