using System;
using System.IO;
using System.Text;
using LapsTool.Core.Configuration;

namespace LapsTool.Core.Logging
{
    /// <summary>
    /// 操作ログ（誰が・いつ・どの端末に・何をしたか）。パスワードは絶対に書かない。
    /// 保存先: %APPDATA%\LapsTool\logs\operations_yyyyMM.log
    /// </summary>
    public sealed class OperationLog
    {
        private readonly object _gate = new object();

        public string Directory
        {
            get { return Path.Combine(SettingsStore.AppDataDirectory, "logs"); }
        }

        public void Write(string action, string computer, string result)
        {
            var line = string.Join("\t",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Environment.UserDomainName + "\\" + Environment.UserName,
                Sanitize(action),
                Sanitize(computer),
                Sanitize(result));

            try
            {
                lock (_gate)
                {
                    System.IO.Directory.CreateDirectory(Directory);
                    var path = Path.Combine(Directory, "operations_" + DateTime.Now.ToString("yyyyMM") + ".log");
                    File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch (IOException)
            {
                // ログが書けないことで本来の操作を止めない。
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string Sanitize(string value)
        {
            return (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
