using System;
using System.IO;
using System.Xml.Serialization;

namespace LapsTool.Core.Configuration
{
    /// <summary>
    /// 設定の読み書き。
    ///
    /// 読み込みの優先順位:
    ///   1. %APPDATA%\LapsTool\settings.xml（利用者が保存した値）
    ///   2. exe と同じフォルダの LapsTool.defaults.xml（社内配布時に管理者が置く既定値）
    ///   3. <see cref="AppSettings"/> の組み込み既定値
    ///
    /// 2 があることで、ローカルアカウント名などを会社ごとに揃えて配布できる。
    /// </summary>
    public static class SettingsStore
    {
        public const string DefaultsFileName = "LapsTool.defaults.xml";

        public static string AppDataDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LapsTool");
            }
        }

        public static string UserSettingsPath
        {
            get { return Path.Combine(AppDataDirectory, "settings.xml"); }
        }

        public static string DefaultsPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultsFileName); }
        }

        public static AppSettings Load()
        {
            var settings = TryRead(UserSettingsPath) ?? TryRead(DefaultsPath) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }

        public static void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Directory.CreateDirectory(AppDataDirectory);
            WriteTo(UserSettingsPath, settings);
        }

        /// <summary>
        /// 任意のパスへ書き出す。社内配布用の LapsTool.defaults.xml を作るときにも使う。
        /// 途中で落ちても既存ファイルが壊れないよう、一時ファイルに書いてから置き換える。
        /// </summary>
        public static void WriteTo(string path, AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            settings.Normalize();
            var tempPath = path + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                new XmlSerializer(typeof(AppSettings)).Serialize(stream, settings);
            }

            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }

        private static AppSettings TryRead(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    return (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(stream);
                }
            }
            catch (InvalidOperationException)
            {
                // 壊れた設定ファイルで起動不能にしない。次の候補へフォールバックする。
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
