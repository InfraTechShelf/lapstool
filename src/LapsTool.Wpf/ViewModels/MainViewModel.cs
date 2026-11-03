using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using LapsTool.Core.Configuration;
using LapsTool.Core.Laps;
using LapsTool.Core.Logging;
using LapsTool.Core.Rdp;
using LapsTool.Wpf.Mvvm;
using LapsTool.Wpf.Services;

namespace LapsTool.Wpf.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private const string Masked = "●●●●●●●●●●";
        private const string DateFormat = "yyyy/MM/dd HH:mm";

        private readonly IUserDialogs _dialogs;
        private readonly OperationLog _log = new OperationLog();
        private readonly PasswordClipboard _clipboard = new PasswordClipboard();
        private readonly RdpLauncher _rdp;

        private AppSettings _settings;
        private LapsService _service;

        private string _searchText = "";
        private LapsComputer _selectedComputer;
        private LapsPassword _password;
        private bool _isPasswordShown;
        private DateTime? _newExpirationDate;
        private string _newExpirationTime = "";
        private bool _isBusy;
        private string _statusText = "コンピューター名を入力して検索してください（* でワイルドカード）。";

        public MainViewModel(IUserDialogs dialogs)
        {
            if (dialogs == null) throw new ArgumentNullException(nameof(dialogs));
            _dialogs = dialogs;
            _rdp = new RdpLauncher(_log);

            Computers = new ObservableCollection<LapsComputer>();

            SearchCommand = new RelayCommand(Search, () => !IsBusy);
            GetPasswordCommand = new RelayCommand(GetPassword, () => !IsBusy && IsManaged);
            TogglePasswordCommand = new RelayCommand(() => IsPasswordShown = !IsPasswordShown, () => _password != null);
            CopyPasswordCommand = new RelayCommand(CopyPassword, () => _password != null);
            SetExpirationCommand = new RelayCommand(SetExpiration, () => !IsBusy && IsManaged);
            ExpireNowCommand = new RelayCommand(ExpireNow, () => !IsBusy && IsManaged);
            ConnectCommand = new RelayCommand(Connect, () => !IsBusy && IsManaged);

            ApplySettings(SettingsStore.Load());
        }

        public ObservableCollection<LapsComputer> Computers { get; }

        public RelayCommand SearchCommand { get; }
        public RelayCommand GetPasswordCommand { get; }
        public RelayCommand TogglePasswordCommand { get; }
        public RelayCommand CopyPasswordCommand { get; }
        public RelayCommand SetExpirationCommand { get; }
        public RelayCommand ExpireNowCommand { get; }
        public RelayCommand ConnectCommand { get; }

        public AppSettings Settings
        {
            get { return _settings; }
        }

        public string SearchText
        {
            get { return _searchText; }
            set { SetProperty(ref _searchText, value); }
        }

        public LapsComputer SelectedComputer
        {
            get { return _selectedComputer; }
            set
            {
                if (!SetProperty(ref _selectedComputer, value)) return;
                ClearPassword();
                ResetNewExpiration();
                RaiseDetails();
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                if (SetProperty(ref _isBusy, value)) RaiseCommands();
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value); }
        }

        public bool HasSelection
        {
            get { return _selectedComputer != null; }
        }

        /// <summary>ワイルドカード検索で複数ヒットしたときだけ候補一覧を出す。1件なら自動選択で足りる。</summary>
        public bool HasMultipleResults
        {
            get { return Computers.Count > 1; }
        }

        private bool IsManaged
        {
            get { return _selectedComputer != null && _selectedComputer.Kind != LapsKind.None; }
        }

        // ---- 詳細表示 ----

        public string ComputerDisplay
        {
            get
            {
                if (_selectedComputer == null) return "";
                return string.IsNullOrEmpty(_selectedComputer.DnsHostName)
                    ? _selectedComputer.Name
                    : _selectedComputer.Name + "（" + _selectedComputer.DnsHostName + "）";
            }
        }

        public string KindDisplay
        {
            get { return _selectedComputer == null ? "" : _selectedComputer.KindDisplay; }
        }

        public string AccountDisplay
        {
            get
            {
                if (_password == null) return "（パスワード取得後に表示）";
                return _password.Account ?? "（旧LAPSは記録なし）";
            }
        }

        public string PasswordDisplay
        {
            get
            {
                if (_password == null) return "";
                return _isPasswordShown ? PasswordClipboard.ToPlainText(_password.Password) : Masked;
            }
        }

        public bool IsPasswordShown
        {
            get { return _isPasswordShown; }
            set
            {
                if (SetProperty(ref _isPasswordShown, value))
                {
                    RaisePropertyChanged(nameof(PasswordDisplay));
                    RaisePropertyChanged(nameof(ToggleLabel));
                }
            }
        }

        public string ToggleLabel
        {
            get { return _isPasswordShown ? "隠す" : "表示"; }
        }

        public string PasswordUpdateDisplay
        {
            get { return _password == null ? "" : Format(_password.PasswordUpdateTime); }
        }

        public string ExpirationDisplay
        {
            get
            {
                if (_selectedComputer == null) return "";
                var expiry = _selectedComputer.ExpirationTime;
                if (!expiry.HasValue) return "-";
                var text = expiry.Value.ToString(DateFormat);
                return expiry.Value <= DateTime.Now ? text + "（期限切れ・次回ポリシー処理で更新）" : text;
            }
        }

        public string ConnectUserDisplay
        {
            get
            {
                if (_selectedComputer == null) return "";
                return RdpLauncher.ResolveAddress(_selectedComputer, _settings) + " に " +
                       RdpLauncher.ResolveUserName(_selectedComputer, _password, _settings) + " で接続";
            }
        }

        public DateTime? NewExpirationDate
        {
            get { return _newExpirationDate; }
            set { SetProperty(ref _newExpirationDate, value); }
        }

        /// <summary>"HH:mm" 形式。</summary>
        public string NewExpirationTime
        {
            get { return _newExpirationTime; }
            set { SetProperty(ref _newExpirationTime, value); }
        }

        // ---- 設定 ----

        public void ApplySettings(AppSettings settings)
        {
            _settings = settings;
            _service = new LapsService(settings, _log);
            RaisePropertyChanged(nameof(Settings));
            RaiseDetails();
        }

        /// <summary>終了時の後始末。クリップボードにパスワードを残さない。</summary>
        public void Shutdown()
        {
            _clipboard.ClearIfOurs();
            ClearPassword();
        }

        // ---- 操作 ----

        private async void Search()
        {
            var query = SearchText;
            await RunAsync("検索中…", async () =>
            {
                var results = await Task.Run(() => _service.Search(query));
                Computers.Clear();
                foreach (var c in results) Computers.Add(c);
                RaisePropertyChanged(nameof(HasMultipleResults));
                SelectedComputer = Computers.Count == 1 ? Computers[0] : null;

                StatusText = results.Count == 0
                    ? "該当するコンピューターがありません。"
                    : results.Count == 1
                        ? results[0].Name + " が見つかりました。"
                        : results.Count >= _settings.MaxSearchResults
                            ? results.Count + " 件（上限に達しました。条件を絞ってください）"
                            : results.Count + " 件見つかりました。一覧から選んでください。";
            });
        }

        private async void GetPassword()
        {
            await RunAsync("パスワードを取得中…", async () =>
            {
                await FetchPasswordAsync();
                StatusText = _selectedComputer.Name + " のパスワードを取得しました。";
            });
        }

        private void CopyPassword()
        {
            _clipboard.Copy(_password.Password, _settings.ClipboardClearSeconds);
            _log.Write("パスワードコピー", _password.ComputerName, "クリップボードへコピー");
            StatusText = "クリップボードにコピーしました（" + _settings.ClipboardClearSeconds + " 秒後に消去）。";
        }

        private async void SetExpiration()
        {
            DateTime when;
            if (!TryGetNewExpiration(out when))
            {
                _dialogs.ShowError("有効期限の日付と時刻（HH:mm）を正しく入力してください。");
                return;
            }

            var message = _selectedComputer.Name + " のパスワード有効期限を " + when.ToString(DateFormat) +
                          " に変更します。";
            if (when <= DateTime.Now)
                message += "\n\n過去の日時のため、次回のポリシー処理でパスワードが変更されます。";
            if (!_dialogs.Confirm(message + "\n\nよろしいですか？")) return;

            await ChangeExpirationAsync(when);
        }

        private async void ExpireNow()
        {
            if (!_dialogs.Confirm(_selectedComputer.Name + " のパスワードを今すぐ期限切れにします。\n\n" +
                                  "対象端末で次にポリシーが処理されたとき（通常1時間以内）にパスワードが変更されます。\n" +
                                  "よろしいですか？"))
                return;

            await ChangeExpirationAsync(DateTime.Now);
        }

        private async void Connect()
        {
            await RunAsync("接続を準備中…", async () =>
            {
                // アカウント名（Windows LAPS の記録を優先する場合）とコピーするパスワードのために先に取得する。
                if (_password == null)
                    await FetchPasswordAsync();

                _rdp.Launch(_selectedComputer, _password, _settings);

                if (_settings.CopyPasswordOnRdp)
                {
                    _clipboard.Copy(_password.Password, _settings.ClipboardClearSeconds);
                    _log.Write("パスワードコピー", _password.ComputerName, "RDP接続時にクリップボードへコピー");
                    StatusText = "mstsc を起動しました。パスワード欄に貼り付けてください（クリップボードは " +
                                 _settings.ClipboardClearSeconds + " 秒後に消去）。";
                }
                else
                {
                    StatusText = "mstsc を起動しました。パスワードを入力してください。";
                }
            });
        }

        private async Task ChangeExpirationAsync(DateTime when)
        {
            await RunAsync("有効期限を変更中…", async () =>
            {
                var target = _selectedComputer;
                var updated = await Task.Run(() => _service.SetExpiration(target, when));
                ReplaceSelected(updated);

                StatusText = updated.Name + " の有効期限を変更しました。";
            });
        }

        /// <summary>
        /// 選択中のコンピューターを新しい値に差し替える。
        /// 一覧の要素を差し替えると ListBox が選択を外し、取得済みパスワードが破棄されるので、退避して戻す。
        /// </summary>
        private void ReplaceSelected(LapsComputer updated)
        {
            var keep = _password;
            _password = null;
            var index = Computers.IndexOf(_selectedComputer);
            if (index >= 0) Computers[index] = updated;
            _selectedComputer = updated;
            _password = keep;
            RaisePropertyChanged(nameof(SelectedComputer));
            ResetNewExpiration();
            RaiseDetails();
        }

        private async Task FetchPasswordAsync()
        {
            var target = _selectedComputer;
            var password = await Task.Run(() => _service.GetPassword(target));

            // 取得中に別の端末を選んでいたら捨てる。
            if (!ReferenceEquals(target, _selectedComputer))
            {
                password.Dispose();
                throw new LapsException("取得中に選択が変わったため中止しました。");
            }

            ClearPassword();
            _password = password;

            // 検索した後にローテーションされていると、検索時点の有効期限は古い。
            // パスワードと同時に取れた最新の期限で表示を更新する（旧 LAPS も同時に読んでいる）。
            if (password.ExpirationTime.HasValue && password.ExpirationTime != target.ExpirationTime)
                ReplaceSelected(target.WithExpiration(password.ExpirationTime));
            else
                RaiseDetails();
        }

        private async Task RunAsync(string busyText, Func<Task> action)
        {
            IsBusy = true;
            StatusText = busyText;
            try
            {
                await action();
            }
            catch (LapsException ex)
            {
                StatusText = ex.Message;
                _dialogs.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                StatusText = "エラー: " + ex.Message;
                _dialogs.ShowError("予期しないエラーが発生しました。\n\n" + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool TryGetNewExpiration(out DateTime when)
        {
            when = DateTime.MinValue;
            if (!_newExpirationDate.HasValue) return false;

            TimeSpan time;
            if (!TimeSpan.TryParseExact((_newExpirationTime ?? "").Trim(), @"h\:mm", CultureInfo.InvariantCulture, out time) ||
                time >= TimeSpan.FromDays(1))
                return false;

            when = _newExpirationDate.Value.Date + time;
            return true;
        }

        private void ResetNewExpiration()
        {
            var basis = _selectedComputer != null && _selectedComputer.ExpirationTime.HasValue
                ? _selectedComputer.ExpirationTime.Value
                : DateTime.Now.AddDays(1);
            NewExpirationDate = basis.Date;
            NewExpirationTime = basis.ToString("HH:mm");
        }

        private void ClearPassword()
        {
            if (_password != null)
            {
                _password.Dispose();
                _password = null;
            }
            _isPasswordShown = false;
        }

        private void RaiseDetails()
        {
            RaisePropertyChanged(nameof(HasSelection));
            RaisePropertyChanged(nameof(ComputerDisplay));
            RaisePropertyChanged(nameof(KindDisplay));
            RaisePropertyChanged(nameof(AccountDisplay));
            RaisePropertyChanged(nameof(PasswordDisplay));
            RaisePropertyChanged(nameof(IsPasswordShown));
            RaisePropertyChanged(nameof(ToggleLabel));
            RaisePropertyChanged(nameof(PasswordUpdateDisplay));
            RaisePropertyChanged(nameof(ExpirationDisplay));
            RaisePropertyChanged(nameof(ConnectUserDisplay));
            RaiseCommands();
        }

        private void RaiseCommands()
        {
            SearchCommand.RaiseCanExecuteChanged();
            GetPasswordCommand.RaiseCanExecuteChanged();
            TogglePasswordCommand.RaiseCanExecuteChanged();
            CopyPasswordCommand.RaiseCanExecuteChanged();
            SetExpirationCommand.RaiseCanExecuteChanged();
            ExpireNowCommand.RaiseCanExecuteChanged();
            ConnectCommand.RaiseCanExecuteChanged();
        }

        private static string Format(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString(DateFormat) : "-";
        }
    }
}
