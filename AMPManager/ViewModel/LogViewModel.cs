using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input; // ICommand
// [중요] 모호함 방지를 위해 System.Windows 네임스페이스는 직접 using 하지 않고 풀네임 사용

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

        private string _searchDate = DateTime.Now.ToString("yyyy-MM-dd");
        public string SearchDate { get => _searchDate; set => SetProperty(ref _searchDate, value); }

        private bool _isCheckedNormal = true;
        public bool IsCheckedNormal { get => _isCheckedNormal; set { SetProperty(ref _isCheckedNormal, value); FilterLogs(); } }

        private bool _isCheckedDefect = true;
        public bool IsCheckedDefect { get => _isCheckedDefect; set { SetProperty(ref _isCheckedDefect, value); FilterLogs(); } }

        public ICommand SearchCommand { get; }
        public ICommand OpenDetailCommand { get; }

        public LogViewModel()
        {
            SearchCommand = new RelayCommand(o => LoadData());
            OpenDetailCommand = new RelayCommand(OpenDetailWindow);
        }

        private void LoadData()
        {
            _allLogs.Clear();
            string formattedDate = SearchDate;
            if (formattedDate.Contains('.')) formattedDate = formattedDate.Replace('.', '-');
            if (DateTime.TryParse(formattedDate, out DateTime parsedDate)) formattedDate = parsedDate.ToString("yyyy-MM-dd");

            var logs = _dbManager.GetLogsDirect(formattedDate);

            if (logs == null) return;
            if (logs.Count == 0 && formattedDate.ToUpper() != "ALL")
            {
                // [중요] 명시적 호출
                System.Windows.MessageBox.Show($"'{formattedDate}' 날짜의 데이터가 없습니다.", "알림");
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
                var (imgBytes1, imgBytes2) = _dbManager.GetLogImages(log.MeasureId);
                log.Img1 = ByteToImage(imgBytes1);
                log.Img2 = ByteToImage(imgBytes2);

                var window = new LogDetailWindow(log);

                // [중요] 명시적 호출
                if (System.Windows.Application.Current.MainWindow != null)
                {
                    window.Owner = System.Windows.Application.Current.MainWindow;
                }
                window.ShowDialog();
            }
        }

        private System.Windows.Media.ImageSource? ByteToImage(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                var image = new System.Windows.Media.Imaging.BitmapImage();
                using (var mem = new MemoryStream(bytes))
                {
                    mem.Position = 0;
                    image.BeginInit();
                    image.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
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