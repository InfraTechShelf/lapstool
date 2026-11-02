using System.Windows;

namespace LapsTool.Wpf.Services
{
    /// <summary>ViewModel から確認・エラー表示を行うための窓口。</summary>
    public interface IUserDialogs
    {
        bool Confirm(string message);

        void ShowError(string message);
    }

    public sealed class MessageBoxDialogs : IUserDialogs
    {
        private readonly Window _owner;

        public MessageBoxDialogs(Window owner)
        {
            _owner = owner;
        }

        public bool Confirm(string message)
        {
            return MessageBox.Show(_owner, message, "LapsTool", MessageBoxButton.OKCancel,
                MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK;
        }

        public void ShowError(string message)
        {
            MessageBox.Show(_owner, message, "LapsTool", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
