using System;
using System.Windows;
using LapsTool.Core.Configuration;
using LapsTool.Wpf.Services;
using LapsTool.Wpf.ViewModels;

namespace LapsTool.Wpf.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(new MessageBoxDialogs(this));
            DataContext = _vm;
        }

        protected override void OnClosed(EventArgs e)
        {
            _vm.Shutdown();
            base.OnClosed(e);
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow(_vm.Settings.Clone()) { Owner = this };
            if (window.ShowDialog() != true) return;

            try
            {
                SettingsStore.Save(window.Result);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "設定を保存できませんでした。\n\n" + ex.Message, "LapsTool",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _vm.ApplySettings(window.Result);
        }
    }
}
