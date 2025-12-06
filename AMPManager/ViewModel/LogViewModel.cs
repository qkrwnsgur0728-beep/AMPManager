using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AMPManager.Core;
using AMPManager.Model;
using AMPManager.View;

namespace AMPManager.ViewModel
{
    public class LogViewModel : BaseViewModel
    {
        private ApiService _apiService = new ApiService();

        private List<LogEntry> _allLogs = new List<LogEntry>();
        public ObservableCollection<LogEntry> LogData { get; } = new ObservableCollection<LogEntry>();

        private DateTime _searchDate = DateTime.Now;
        public DateTime SearchDate { get => _searchDate; set => SetProperty(ref _searchDate, value); }

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

            // [변경 전] 초기 로드 수행
            // LoadData();

            // [변경 후] 자동 로드 제거 (사용자가 조회 버튼을 눌러야 함)
        }

        private async void LoadData()
        {
            _allLogs.Clear();

            // API에 보낼 날짜 문자열 변환 (yyyy-MM-dd)
            string formattedDate = SearchDate.ToString("yyyy-MM-dd");

            // 비동기 API 호출로 로그 조회
            var logs = await _apiService.GetLogsAsync(formattedDate);

            if (logs == null) return;
            if (logs.Count == 0)
            {
                System.Windows.MessageBox.Show($"'{formattedDate}' 날짜의 데이터가 없습니다.", "알림");
            }

            foreach (var log in logs)
            {
                // 서버에서 DefectReason이 비어있을 경우 기본값 처리
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

        private async void OpenDetailWindow(object? parameter)
        {
            if (parameter is LogEntry log)
            {
                // 상세 이미지도 API로 비동기 요청
                var (imgBytes1, imgBytes2) = await _apiService.GetLogImagesAsync(log.MeasureId);

                log.Img1 = ByteToImage(imgBytes1);
                log.Img2 = ByteToImage(imgBytes2);

                var window = new LogDetailWindow(log);

                // 메인 윈도우 중앙에 띄우기
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