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
        // [수정 1] ApiService 변수 선언 및 초기화 추가 (이게 없으면 컴파일 에러가 납니다!)
        private ApiService _apiService = new ApiService();

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
            SearchCommand = new RelayCommand(o => LoadData(true));
            OpenDetailCommand = new RelayCommand(OpenDetailWindow);

            LoadData(false);
        }

        private async void LoadData(bool showMessage = true)
        {
            _allLogs.Clear();
            LogData.Clear();

            string formattedDate = SearchDate;

            if (formattedDate.Contains('.')) formattedDate = formattedDate.Replace('.', '-');
            if (DateTime.TryParse(formattedDate, out DateTime parsedDate)) formattedDate = parsedDate.ToString("yyyy-MM-dd");

            var logs = await _apiService.GetLogsAsync(formattedDate);

            if (logs == null) return;

            if (logs.Count == 0 && formattedDate.ToUpper() != "ALL")
            {
                if (showMessage)
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

        private async void OpenDetailWindow(object? parameter)
        {
            if (parameter is LogEntry log)
            {
                // 1. 서버 데이터 가져오기
                var detailLog = await _apiService.GetLogDetailAsync(log.Id);

                if (detailLog != null)
                {
                    log.MeasuredContour = detailLog.MeasuredContour;
                    log.MeasuredCenter = detailLog.MeasuredCenter;
                    log.TemplateData = detailLog.TemplateData;
                    log.TolShape = detailLog.TolShape;
                    log.TolHole = detailLog.TolHole;
                    log.LimitWarn = detailLog.LimitWarn;
                    log.LimitFail = detailLog.LimitFail;
                    log.HoleOffset = detailLog.HoleOffset;
                    log.AreaSize = detailLog.AreaSize;
                    log.DefectReason = detailLog.DefectReason;
                }

                // 2. 이미지 가져오기
                var (imgBytes1, imgBytes2) = await _apiService.GetLogImagesAsync(log.Id);
                log.Img1 = ByteToImage(imgBytes1);
                log.Img2 = ByteToImage(imgBytes2);

                // 3. 창 열기
                var window = new LogDetailWindow(log);

                // Owner 설정 (Null 체크 포함)
                if (System.Windows.Application.Current != null &&
                    System.Windows.Application.Current.MainWindow != null)
                {
                    window.Owner = System.Windows.Application.Current.MainWindow;
                }

                // [수정 2] 창을 화면에 띄우는 코드 추가 (이게 없으면 버튼 눌러도 반응이 없습니다)
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