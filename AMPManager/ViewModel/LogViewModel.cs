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
        // ★★★ ApiService 대신 DatabaseManager를 사용하도록 변경 ★★★
        // 이 DatabaseManager는 DB에서 직접 데이터를 가져옵니다.
        private DatabaseManager _dbManager = new DatabaseManager();

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
            // ★★★ FIX: 누락된 OpenDetailWindow 메서드를 Command에 연결 ★★★
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

            // ★★★ FIX: C# DB 직접 호출 (GetLogsDirect 함수 사용) ★★★
            var logs = _dbManager.GetLogsDirect(formattedDate);

            // 결과 처리
            if (logs == null)
            {
                // DBManager에서 이미 오류 메시지를 띄웠을 수 있습니다.
                return;
            }
            if (logs.Count == 0)
            {
                // 비상 테스트용 'ALL' 입력 시 메시지 출력 방지
                if (formattedDate.ToUpper() != "ALL")
                {
                    System.Windows.MessageBox.Show($"'{formattedDate}' 날짜의 데이터가 없습니다.", "알림");
                }
            }

            foreach (var log in logs)
            {
                // 불량 사유는 임시 데이터
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

        // ★★★ FIX: 누락되었던 OpenDetailWindow 메서드 (상세 보기) ★★★
        private async void OpenDetailWindow(object? parameter)
        {
            if (parameter is LogEntry log)
            {
                // 서버에서 이미지 데이터 가져오기 (DatabaseManager가 직접 처리)
                var (imgBytes1, imgBytes2) = _dbManager.GetLogImages(log.Id);

                // 이미지 변환
                log.Img1 = ByteToImage(imgBytes1);
                log.Img2 = ByteToImage(imgBytes2);

                // 상세 창 띄우기
                var window = new LogDetailWindow(log);
                window.Owner = System.Windows.Application.Current.MainWindow;
                window.ShowDialog();
            }
        }

        // ★★★ FIX: 누락되었던 ByteToImage 메서드 (이미지 변환) ★★★
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