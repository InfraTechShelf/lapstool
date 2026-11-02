using System;
using System.Collections.Generic;
using LapsTool.Core.Configuration;
using LapsTool.Core.Logging;

namespace LapsTool.Core.Laps
{
    /// <summary>
    /// GUI から呼ぶ窓口。LAPS の種類に応じて処理を振り分け、操作ログを残す。
    /// </summary>
    public sealed class LapsService
    {
        private readonly ComputerDirectory _directory;
        private readonly WindowsLapsClient _windowsLaps;
        private readonly LegacyLapsClient _legacyLaps;
        private readonly OperationLog _log;

        public LapsService(AppSettings settings, OperationLog log)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (log == null) throw new ArgumentNullException(nameof(log));

            _directory = new ComputerDirectory(settings);
            _windowsLaps = new WindowsLapsClient(settings);
            _legacyLaps = new LegacyLapsClient(settings, _directory);
            _log = log;
        }

        public IList<LapsComputer> Search(string query)
        {
            return _directory.Search(query);
        }

        public LapsPassword GetPassword(LapsComputer computer)
        {
            if (computer == null) throw new ArgumentNullException(nameof(computer));

            try
            {
                LapsPassword result;
                switch (computer.Kind)
                {
                    case LapsKind.WindowsLaps: result = _windowsLaps.GetPassword(computer); break;
                    case LapsKind.LegacyLaps: result = _legacyLaps.GetPassword(computer); break;
                    default: throw new LapsException(computer.Name + " は LAPS で管理されていません。");
                }
                _log.Write("パスワード参照", computer.Name, "成功");
                return result;
            }
            catch (Exception ex)
            {
                _log.Write("パスワード参照", computer.Name, "失敗: " + ex.Message);
                throw;
            }
        }

        /// <summary>有効期限を変更し、変更後の値を AD から読み直して返す。</summary>
        public LapsComputer SetExpiration(LapsComputer computer, DateTime whenEffective)
        {
            if (computer == null) throw new ArgumentNullException(nameof(computer));

            var detail = whenEffective.ToString("yyyy/MM/dd HH:mm");
            try
            {
                switch (computer.Kind)
                {
                    case LapsKind.WindowsLaps: _windowsLaps.SetExpiration(computer, whenEffective); break;
                    case LapsKind.LegacyLaps: _legacyLaps.SetExpiration(computer, whenEffective); break;
                    default: throw new LapsException(computer.Name + " は LAPS で管理されていません。");
                }
                _log.Write("有効期限変更", computer.Name, "成功: " + detail);
            }
            catch (Exception ex)
            {
                _log.Write("有効期限変更", computer.Name, "失敗(" + detail + "): " + ex.Message);
                throw;
            }

            return _directory.Refresh(computer);
        }
    }
}
