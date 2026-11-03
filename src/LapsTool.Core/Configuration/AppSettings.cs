using System;

namespace LapsTool.Core.Configuration
{
    /// <summary>
    /// 利用者が変更できる設定。XmlSerializer で保存するため public な既定コンストラクタと
    /// 読み書き可能なプロパティだけで構成する。
    /// </summary>
    [Serializable]
    public class AppSettings
    {
        // ---- リモートデスクトップ接続 ----

        /// <summary>
        /// 接続に使うローカルアカウント名。企業ごとに異なる（Administrator を無効化して
        /// 別名の管理用アカウントを LAPS 管理対象にしている環境が多い）ため設定可能にしている。
        /// </summary>
        public string LocalAccountName { get; set; } = "Administrator";

        /// <summary>
        /// Windows LAPS が記録しているアカウント名があれば、<see cref="LocalAccountName"/> より優先する。
        /// 旧 LAPS はアカウント名を記録しないため、その場合は常に設定値を使う。
        /// </summary>
        public bool PreferLapsAccountName { get; set; } = true;

        /// <summary>接続先を FQDN（dNSHostName）で指定する。false ならコンピューター名。</summary>
        public bool ConnectByFqdn { get; set; } = true;

        /// <summary>mstsc を /admin 付きで起動する。</summary>
        public bool RdpAdminMode { get; set; }

        /// <summary>
        /// 接続時にパスワードをクリップボードへコピーし、mstsc の入力画面に貼り付けられるようにする。
        /// 旧版の CopyPasswordOnConnect（既定オフ）とは意味が変わったため、名前を変えて既定オンにしている
        /// （旧版の設定ファイルの値を引き継がないように）。
        /// </summary>
        public bool CopyPasswordOnRdp { get; set; } = true;

        /// <summary>クリップボードにコピーしたパスワードを自動消去するまでの秒数。</summary>
        public int ClipboardClearSeconds { get; set; } = 30;

        // ---- Active Directory ----

        /// <summary>問い合わせ先のドメインコントローラー（空なら自動選択）。</summary>
        public string DomainController { get; set; } = "";

        /// <summary>検索ベース DN（空ならドメインの既定の名前付けコンテキスト）。</summary>
        public string SearchBase { get; set; } = "";

        /// <summary>検索結果の最大件数。</summary>
        public int MaxSearchResults { get; set; } = 200;

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }

        /// <summary>範囲外の値を補正する。手で編集された設定ファイルにも耐えるため。</summary>
        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(LocalAccountName)) LocalAccountName = "Administrator";
            LocalAccountName = LocalAccountName.Trim();
            DomainController = (DomainController ?? "").Trim();
            SearchBase = (SearchBase ?? "").Trim();
            ClipboardClearSeconds = Clamp(ClipboardClearSeconds, 5, 600);
            MaxSearchResults = Clamp(MaxSearchResults, 1, 5000);
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
