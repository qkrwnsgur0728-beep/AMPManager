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
        // [수정] 로컬 DB 대신 API 서비스를 사용합니다.
        private ApiService _apiService = new ApiService();

        private List<LogEntry> _allLogs = new List<LogEntry>();
        public ObservableCollection<LogEntry> LogData { get; } = new ObservableCollection<LogEntry>();

        // 1. 검색 조건
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

        private async void LoadData()
        {
            _allLogs.Clear();

            string formattedDate = SearchDate;

            // YYYY-MM-DD 형식으로 변환 (클라이언트의 입력 형식이 다를 수 있으므로)
            if (formattedDate.Contains('.'))
            {
                formattedDate = formattedDate.Replace('.', '-');
            }
            if (DateTime.TryParse(formattedDate, out DateTime parsedDate))
            {
                formattedDate = parsedDate.ToString("yyyy-MM-dd");
            }

            // [수정] 서버 API 호출 (비동기)
            var logs = await _apiService.GetLogsAsync(formattedDate);

            // 결과 처리
            if (logs == null) return;

            if (logs.Count == 0)
            {
                // 'ALL' 같은 특수 명령어가 아닐 때만 메시지 표시
                if (formattedDate.ToUpper() != "ALL")
                {
                    System.Windows.MessageBox.Show($"'{formattedDate}' 날짜의 데이터가 없습니다.", "알림");
                }
            }

            foreach (var log in logs)
            {
                // 서버 데이터 보정 (비고란 등)
                if (string.IsNullOrEmpty(log.DefectReason))
                    log.DefectReason = (log.Status == "불량") ? "치수 오차 초과" : "-";

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

            foreach (var item in filtered) LogData.Add(item);
        }

        private async void OpenDetailWindow(object? parameter)
        {
            if (parameter is LogEntry log)
            {
                // [수정] 서버에서 이미지 데이터 가져오기 (API 호출)
                // log.Id는 서버 DB의 measure_id에 해당합니다.
                var (imgBytes1, imgBytes2) = await _apiService.GetLogImagesAsync(log.Id);

                // 이미지 변환
                log.Img1 = ByteToImage(imgBytes1);
                log.Img2 = ByteToImage(imgBytes2);

                // 상세 창 띄우기
                var window = new LogDetailWindow(log);
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