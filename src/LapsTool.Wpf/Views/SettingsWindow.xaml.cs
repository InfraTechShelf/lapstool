using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LapsTool.Core.Configuration;
using Microsoft.Win32;

namespace LapsTool.Wpf.Views
{
    /// <summary>
    /// 設定画面。呼び出し側から受け取った複製を直接バインドし、OK のときだけ <see cref="Result"/> として返す。
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            InitializeComponent();
            Result = settings;
            DataContext = settings;
        }

        public AppSettings Result { get; }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (!CommitAndValidate()) return;
            DialogResult = true;
        }

        private void OnExportClick(object sender, RoutedEventArgs e)
        {
            if (!CommitAndValidate()) return;

            var dialog = new SaveFileDialog
            {
                FileName = SettingsStore.DefaultsFileName,
                Filter = "設定ファイル (*.xml)|*.xml",
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                SettingsStore.WriteTo(dialog.FileName, Result.Clone());
                MessageBox.Show(this, "書き出しました。\n\nこのファイルを LapsTool.exe と同じフォルダに置くと、" +
                                      "設定を保存していない利用者の初期値になります。",
                    "LapsTool", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "書き出せませんでした。\n\n" + ex.Message, "LapsTool",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// フォーカス中の TextBox の値を確定させ、数値欄の入力エラーが無いか確かめる。
        /// （既定の LostFocus 更新では、OK を Enter で押したときに最後の入力が反映されないため）
        /// </summary>
        private bool CommitAndValidate()
        {
            var focused = Keyboard.FocusedElement as TextBox;
            if (focused != null)
            {
                var binding = focused.GetBindingExpression(TextBox.TextProperty);
                if (binding != null) binding.UpdateSource();
            }

            if (HasValidationError(this))
            {
                MessageBox.Show(this, "数値の欄に正しい値を入力してください。", "LapsTool",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(Result.LocalAccountName))
            {
                MessageBox.Show(this, "ローカルアカウント名を入力してください。", "LapsTool",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            Result.Normalize();
            return true;
        }

        private static bool HasValidationError(DependencyObject node)
        {
            if (Validation.GetHasError(node)) return true;
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++)
            {
                if (HasValidationError(System.Windows.Media.VisualTreeHelper.GetChild(node, i))) return true;
            }
            return false;
        }
    }
}
