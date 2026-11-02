using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Text;
using LapsTool.Core.Configuration;

namespace LapsTool.Core.Laps
{
    /// <summary>
    /// AD のコンピューターオブジェクト検索。
    /// 読むのは有効期限など公開属性だけで、パスワード属性はここでは読まない。
    /// </summary>
    public sealed class ComputerDirectory
    {
        internal const string WindowsLapsExpirationAttribute = "msLAPS-PasswordExpirationTime";
        internal const string LegacyLapsExpirationAttribute = "ms-Mcs-AdmPwdExpirationTime";

        private readonly AppSettings _settings;

        public ComputerDirectory(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _settings = settings;
        }

        /// <summary>
        /// コンピューター名で検索する。"*" をワイルドカードとして使える。
        /// "." を含む入力は FQDN（dNSHostName）として照合する。
        /// </summary>
        public IList<LapsComputer> Search(string query)
        {
            query = (query ?? "").Trim();
            if (query.Length == 0) throw new LapsException("コンピューター名を入力してください。");

            var value = EscapeKeepingWildcard(query);
            var filter = query.Contains(".")
                ? "(&(objectCategory=computer)(dNSHostName=" + value + "))"
                : "(&(objectCategory=computer)(name=" + value + "))";

            try
            {
                using (var root = new DirectoryEntry(BuildSearchRootPath()))
                using (var searcher = new DirectorySearcher(root, filter))
                {
                    searcher.SizeLimit = _settings.MaxSearchResults;
                    searcher.PageSize = 0;
                    searcher.PropertiesToLoad.AddRange(new[]
                    {
                        "name", "dNSHostName", "distinguishedName", "operatingSystem",
                        WindowsLapsExpirationAttribute, LegacyLapsExpirationAttribute,
                    });
                    searcher.Sort = new SortOption("name", SortDirection.Ascending);

                    var list = new List<LapsComputer>();
                    using (var results = searcher.FindAll())
                    {
                        foreach (SearchResult r in results)
                            list.Add(ToComputer(r));
                    }
                    return list;
                }
            }
            catch (COMException ex)
            {
                throw new LapsException("Active Directory の検索に失敗しました: " + ex.Message, ex);
            }
        }

        /// <summary>1台分を DN で読み直す（期限変更後の再表示用）。</summary>
        public LapsComputer Refresh(LapsComputer computer)
        {
            try
            {
                using (var entry = new DirectoryEntry(BuildPath(computer.DistinguishedName)))
                using (var searcher = new DirectorySearcher(entry, "(objectClass=computer)"))
                {
                    searcher.SearchScope = SearchScope.Base;
                    searcher.PropertiesToLoad.AddRange(new[]
                    {
                        "name", "dNSHostName", "distinguishedName", "operatingSystem",
                        WindowsLapsExpirationAttribute, LegacyLapsExpirationAttribute,
                    });
                    var r = searcher.FindOne();
                    return r == null ? computer : ToComputer(r);
                }
            }
            catch (COMException ex)
            {
                throw new LapsException("コンピューター情報の再取得に失敗しました: " + ex.Message, ex);
            }
        }

        internal string BuildPath(string distinguishedName)
        {
            return string.IsNullOrEmpty(_settings.DomainController)
                ? "LDAP://" + distinguishedName
                : "LDAP://" + _settings.DomainController + "/" + distinguishedName;
        }

        private string BuildSearchRootPath()
        {
            var baseDn = _settings.SearchBase;
            if (string.IsNullOrEmpty(baseDn))
            {
                var rootDsePath = string.IsNullOrEmpty(_settings.DomainController)
                    ? "LDAP://RootDSE"
                    : "LDAP://" + _settings.DomainController + "/RootDSE";
                using (var rootDse = new DirectoryEntry(rootDsePath))
                {
                    baseDn = (string)rootDse.Properties["defaultNamingContext"].Value;
                }
            }
            return BuildPath(baseDn);
        }

        private static LapsComputer ToComputer(SearchResult r)
        {
            var winExpiry = ReadFileTime(r, WindowsLapsExpirationAttribute);
            var legacyExpiry = ReadFileTime(r, LegacyLapsExpirationAttribute);

            // 移行期間は両方の属性が残っていることがある。Windows LAPS を優先する
            // （Windows LAPS がレガシーエミュレーションモードでなければ、旧属性は更新されなくなるため）。
            LapsKind kind;
            DateTime? expiry;
            if (r.Properties.Contains(WindowsLapsExpirationAttribute))
            {
                kind = LapsKind.WindowsLaps;
                expiry = winExpiry;
            }
            else if (r.Properties.Contains(LegacyLapsExpirationAttribute))
            {
                kind = LapsKind.LegacyLaps;
                expiry = legacyExpiry;
            }
            else
            {
                kind = LapsKind.None;
                expiry = null;
            }

            return new LapsComputer(
                ReadString(r, "name"),
                ReadString(r, "dNSHostName"),
                ReadString(r, "distinguishedName"),
                ReadString(r, "operatingSystem"),
                kind,
                expiry);
        }

        private static string ReadString(SearchResult r, string name)
        {
            return r.Properties.Contains(name) ? r.Properties[name][0] as string : null;
        }

        internal static DateTime? ReadFileTime(SearchResult r, string name)
        {
            if (!r.Properties.Contains(name)) return null;
            var raw = r.Properties[name][0];
            long value;
            if (raw is long) value = (long)raw;
            else if (!long.TryParse(Convert.ToString(raw), out value)) return null;
            return FileTimeToLocal(value);
        }

        internal static DateTime? FileTimeToLocal(long fileTime)
        {
            if (fileTime <= 0 || fileTime == long.MaxValue) return null;
            try
            {
                return DateTime.FromFileTimeUtc(fileTime).ToLocalTime();
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>RFC 4515 のエスケープ。ただし "*" はワイルドカードとして残す。</summary>
        private static string EscapeKeepingWildcard(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append(@"\5c"); break;
                    case '(': sb.Append(@"\28"); break;
                    case ')': sb.Append(@"\29"); break;
                    case '\0': sb.Append(@"\00"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
