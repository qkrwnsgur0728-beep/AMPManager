using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AMPManager.Core;
using AMPManager.Model;
using AMPManager.View;

namespace AMPManager.ViewModel
{
    public class LogViewModel : BaseViewModel
    {
        private DatabaseManager _dbManager = new DatabaseManager();

        private List<LogEntry> _allLogs = new List<LogEntry>();
        public ObservableCollection<LogEntry> LogData { get; } = new ObservableCollection<LogEntry>();

        // 날짜 (기본값: 오늘)
        private DateTime _searchDate = DateTime.Now;
        public DateTime SearchDate
        {
            get => _searchDate;
            set { SetProperty(ref _searchDate, value); }
        }

        private bool _isCheckedNormal = true;
        public bool IsCheckedNormal { get => _isCheckedNormal; set { SetProperty(ref _isCheckedNormal, value); FilterLogs(); } }

        private bool _isCheckedDefect = true;
        public bool IsCheckedDefect { get => _isCheckedDefect; set { SetProperty(ref _isCheckedDefect, value); FilterLogs(); } }

        public ICommand SearchCommand { get; }
        public ICommand OpenDetailCommand { get; }

        public LogViewModel()
        {
            // 사용자가 버튼 눌렀을 때 -> 알림창 띄움 (false)
            SearchCommand = new RelayCommand(o => LoadData(false));
            OpenDetailCommand = new RelayCommand(OpenDetailWindow);

            // 프로그램 시작 시 자동 로드 -> 알림창 끔 (true)
            LoadData(true);
        }

        // [수정] isAutoLoad 파라미터 추가 (기본값: false)
        private void LoadData(bool isAutoLoad = false)
        {
            _allLogs.Clear();
            string formattedDate = SearchDate.ToString("yyyy-MM-dd");

            var logs = _dbManager.GetLogsDirect(formattedDate);

            if (logs == null) return;

            if (logs.Count == 0)
            {
                // ★ 자동 로드가 아닐 때만(버튼 눌렀을 때만) 메시지 띄우기
                if (!isAutoLoad)
                {
                    System.Windows.MessageBox.Show($"'{formattedDate}' 날짜의 데이터가 없습니다.", "알림");
                }
            }

            foreach (var log in logs)
            {
                if (string.IsNullOrEmpty(log.DefectReason))
                    log.DefectReason = (log.Status == "불량") ? "치수 오차 초과" : "-";
                _allLogs.Add(log);
            }
            FilterLogs();
        }

        private void FilterLogs()
        {
            LogData.Clear();
            var filtered = _allLogs.Where(x => (IsCheckedNormal && x.Status == "정상") || (IsCheckedDefect && x.Status == "불량"));
            foreach (var item in filtered) LogData.Add(item);
        }

        private void OpenDetailWindow(object? parameter)
        {
            if (parameter is LogEntry log)
            {
                var (imgBytes1, imgBytes2) = _dbManager.GetLogImages(log.Id);
                log.Img1 = ByteToImage(imgBytes1);
                log.Img2 = ByteToImage(imgBytes2);

                var window = new LogDetailWindow(log);
                if (System.Windows.Application.Current.MainWindow != null)
                {
                    window.Owner = System.Windows.Application.Current.MainWindow;
                }
                window.ShowDialog();
            }
        }

        private ImageSource? ByteToImage(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                var image = new BitmapImage();
                using (var mem = new MemoryStream(bytes))
                {
                    mem.Position = 0;
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = mem;
                    image.EndInit();
                }
                image.Freeze();
                return image;
            }
            catch { return null; }
        }
    }
}