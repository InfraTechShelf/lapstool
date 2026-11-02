using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Threading;

namespace LapsTool.Wpf.Services
{
    /// <summary>
    /// パスワードをクリップボードへコピーし、指定秒数後に消す。
    /// 消す時点でクリップボードの中身が変わっていたら（利用者が別の物をコピーした）触らない。
    /// </summary>
    public sealed class PasswordClipboard
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private string _copied;

        public PasswordClipboard()
        {
            _timer.Tick += (s, e) => ClearIfOurs();
        }

        public void Copy(SecureString password, int clearSeconds)
        {
            var text = ToPlainText(password);
            Clipboard.SetText(text);
            _copied = text;

            _timer.Stop();
            _timer.Interval = TimeSpan.FromSeconds(clearSeconds);
            _timer.Start();
        }

        public void ClearIfOurs()
        {
            _timer.Stop();
            if (_copied == null) return;

            try
            {
                if (Clipboard.ContainsText() && Clipboard.GetText() == _copied)
                    Clipboard.Clear();
            }
            catch (COMException)
            {
                // 他のプロセスがクリップボードを開いている。次の機会に任せず諦める。
            }
            _copied = null;
        }

        public static string ToPlainText(SecureString value)
        {
            if (value == null) return "";
            var ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.SecureStringToGlobalAllocUnicode(value);
                return Marshal.PtrToStringUni(ptr) ?? "";
            }
            finally
            {
                if (ptr != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }
    }
}
