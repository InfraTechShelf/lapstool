using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;

namespace LapsTool.Core.Rdp
{
    /// <summary>
    /// mstsc が自動で使う資格情報（ターゲット "TERMSRV/&lt;接続先&gt;"）を
    /// Windows 資格情報マネージャーに一時登録・削除する。
    ///
    /// - cmdkey /pass: はコマンドラインにパスワードが載るため使わず、CredWrite を直接呼ぶ。
    /// - Persist は CRED_PERSIST_SESSION。削除に失敗してもログオフで消える。
    /// - パスワードは SecureString から非管理メモリへ直接展開し、使用後にゼロクリアする。
    /// </summary>
    public static class TemporaryRdpCredential
    {
        private const int CRED_TYPE_DOMAIN_PASSWORD = 2;
        private const int CRED_PERSIST_SESSION = 1;
        private const int ERROR_NOT_FOUND = 1168;

        public static string TargetFor(string address)
        {
            return "TERMSRV/" + address;
        }

        public static bool Exists(string address)
        {
            IntPtr ptr;
            if (!NativeMethods.CredRead(TargetFor(address), CRED_TYPE_DOMAIN_PASSWORD, 0, out ptr))
                return false;
            NativeMethods.CredFree(ptr);
            return true;
        }

        public static void Write(string address, string userName, SecureString password)
        {
            if (string.IsNullOrEmpty(address)) throw new ArgumentNullException(nameof(address));
            if (string.IsNullOrEmpty(userName)) throw new ArgumentNullException(nameof(userName));
            if (password == null) throw new ArgumentNullException(nameof(password));

            var blob = IntPtr.Zero;
            try
            {
                blob = Marshal.SecureStringToGlobalAllocUnicode(password);

                var credential = new NativeMethods.CREDENTIAL
                {
                    Type = CRED_TYPE_DOMAIN_PASSWORD,
                    TargetName = TargetFor(address),
                    Comment = "LapsTool による一時登録（接続後に自動削除）",
                    CredentialBlobSize = password.Length * 2, // UTF-16、終端 NUL は含めない
                    CredentialBlob = blob,
                    Persist = CRED_PERSIST_SESSION,
                    UserName = userName,
                };

                if (!NativeMethods.CredWrite(ref credential, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "資格情報の一時登録に失敗しました。");
            }
            finally
            {
                if (blob != IntPtr.Zero)
                    Marshal.ZeroFreeGlobalAllocUnicode(blob);
            }
        }

        /// <summary>削除する。既に無い場合は何もしない。</summary>
        public static void Delete(string address)
        {
            if (NativeMethods.CredDelete(TargetFor(address), CRED_TYPE_DOMAIN_PASSWORD, 0))
                return;

            var error = Marshal.GetLastWin32Error();
            if (error != ERROR_NOT_FOUND)
                throw new Win32Exception(error, "一時登録した資格情報の削除に失敗しました。");
        }

        private static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct CREDENTIAL
            {
                public int Flags;
                public int Type;
                public string TargetName;
                public string Comment;
                public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
                public int CredentialBlobSize;
                public IntPtr CredentialBlob;
                public int Persist;
                public int AttributeCount;
                public IntPtr Attributes;
                public string TargetAlias;
                public string UserName;
            }

            [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredWrite(ref CREDENTIAL credential, int flags);

            [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

            [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredDelete(string target, int type, int flags);

            [DllImport("advapi32.dll")]
            public static extern void CredFree(IntPtr buffer);
        }
    }
}
