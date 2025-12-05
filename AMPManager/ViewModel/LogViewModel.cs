using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
// using System.Windows; // 모호함 방지를 위해 제거 (필요시 명시적 사용)

using AMPManager.Core;  // DatabaseManager, RelayCommand 위치
using AMPManager.Model; // LogEntry 위치
using AMPManager.View;  // LogDetailWindow 위치

namespace AMPManager.ViewModel
{
    public class LogViewModel : BaseViewModel
    {
        // DB 매니저
        private DatabaseManager _dbManager = new DatabaseManager();

        // 데이터 리스트
        private List<LogEntry> _allLogs = new List<LogEntry>();
        public ObservableCollection<LogEntry> LogData { get; } = new ObservableCollection<LogEntry>();

        // 1. 검색 조건
        private string _searchDate = DateTime.Now.ToString("yyyy-MM-dd");
        public string SearchDate { get => _searchDate; set => SetProperty(ref _searchDate, value); }

        // 2. 필터 체크박스
        private bool _isCheckedNormal = true;
        public bool IsCheckedNormal
        {
            get => _isCheckedNormal;
            set { SetProperty(ref _isCheckedNormal, value); FilterLogs(); }
        }

        private bool _isCheckedDefect = true;
        public bool IsCheckedDefect
        {
            get => _isCheckedDefect;
            set { SetProperty(ref _isCheckedDefect, value); FilterLogs(); }
        }

        // 3. 커맨드
        public ICommand SearchCommand { get; }
        public ICommand OpenDetailCommand { get; }

        public LogViewModel()
        {
            SearchCommand = new RelayCommand(o => LoadData());
            OpenDetailCommand = new RelayCommand(OpenDetailWindow);
        }

        // 데이터 로드
        private void LoadData()
        {
            _allLogs.Clear();

            string formattedDate = SearchDate;
            if (formattedDate.Contains('.')) formattedDate = formattedDate.Replace('.', '-');
            if (DateTime.TryParse(formattedDate, out DateTime parsedDate))
            {
                formattedDate = parsedDate.ToString("yyyy-MM-dd");
            }

            var logs = _dbManager.GetLogsDirect(formattedDate);

            if (logs == null) return;

            if (logs.Count == 0 && formattedDate.ToUpper() != "ALL")
            {
                // [충돌 해결] System.Windows 명시
                System.Windows.MessageBox.Show($"'{formattedDate}' 날짜의 데이터가 없습니다.", "알림");
            }

            foreach (var log in logs)
            {
                if (string.IsNullOrEmpty(log.DefectReason))
                {
                    log.DefectReason = (log.Status == "불량") ? "치수 오차 초과" : "-";
                }
                _allLogs.Add(log);
            }
            FilterLogs();
        }

        private void FilterLogs()
        {
            LogData.Clear();
            var filtered = _allLogs.Where(x =>
                (IsCheckedNormal && x.Status == "정상") ||
                (IsCheckedDefect && x.Status == "불량")
            );

            foreach (var item in filtered)
            {
                LogData.Add(item);
            }
        }

        // 상세 창 열기
        private void OpenDetailWindow(object? parameter)
        {
            if (parameter is LogEntry log)
            {
                var window = new LogDetailWindow(log);

                // ★★★ [충돌 해결] System.Windows.Application 명시 ★★★
                if (System.Windows.Application.Current.MainWindow != null)
                {
                    window.Owner = System.Windows.Application.Current.MainWindow;
                }

                window.ShowDialog();
            }
        }
    }
}