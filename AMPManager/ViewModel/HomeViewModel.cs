using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using AMPManager.Core;
using AMPManager.Model;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace AMPManager.ViewModel
{
    public class HomeViewModel : BaseViewModel
    {
        private DispatcherTimer _timer;

        private ApiService _apiService = new ApiService();
        private DatabaseManager _dbManager = new DatabaseManager();
        private MqttService _mqttService = new MqttService();

        private WebSocketImageService _wsService1 = new WebSocketImageService();
        private WebSocketImageService _wsService2 = new WebSocketImageService();

        public PlotModel CombinedChartModel { get; private set; }

        private ImageSource? _cameraImage1;
        private ImageSource? _cameraImage2;

        public ImageSource? CameraImage1 { get => _cameraImage1; set => SetProperty(ref _cameraImage1, value); }
        public ImageSource? CameraImage2 { get => _cameraImage2; set => SetProperty(ref _cameraImage2, value); }

        private int _allocationCount = 1000;
        private int _currentComplete = 0;
        private double _defectRate = 0;
        private int _defectCount = 0;

        public int DefectCount { get => _defectCount; set => SetProperty(ref _defectCount, value); }
        public int AllocationCount { get => _allocationCount; set => SetProperty(ref _allocationCount, value); }
        public int CurrentComplete { get => _currentComplete; set => SetProperty(ref _currentComplete, value); }
        public double DefectRate { get => _defectRate; set => SetProperty(ref _defectRate, value); }

        public HomeViewModel()
        {
            InitializeCombinedChart();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;

            // 실시간 판정 결과 수신 (Server -> MQTT -> WPF)
            _mqttService.MessageReceived += OnMqttDataReceived;
            // MQTT 연결
            _ = _mqttService.ConnectAsync();

            _wsService1.OnImageReceived += HandleImage1;
            _wsService2.OnImageReceived += HandleImage2;
        }

        // ★★★ [수정됨] 차트 초기화 메서드 (축 공유 설정) ★★★
        private void InitializeCombinedChart()
        {
            var textColor = OxyColor.Parse("#E0E0E0");
            var gridColor = OxyColor.Parse("#4A4A5A");

            CombinedChartModel = new PlotModel { Title = "" };
            CombinedChartModel.Background = OxyColors.Transparent;
            CombinedChartModel.PlotAreaBorderColor = OxyColors.Transparent;
            CombinedChartModel.TextColor = textColor;

            // 1. X축 (시간)
            CombinedChartModel.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm:ss",
                AxislineColor = gridColor,
                TicklineColor = gridColor,
                TextColor = textColor,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = gridColor
            });

            // 2. Y축 (왼쪽 - 수량) 
            // ★ 수정: "검사량"과 "불량 개수"가 이 하나의 축을 공유합니다.
            CombinedChartModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Key = "CountAxis",  // 이 키를 두 그래프가 모두 사용함
                Title = "수량 (개)",
                AxislineColor = OxyColor.Parse("#00C1D4"),
                TextColor = OxyColor.Parse("#00C1D4"),
                Minimum = 0
            });

            // ★ 수정: 오른쪽 Y축(DefectAxis) 제거함 (이제 왼쪽 축을 같이 씀)

            // 3. 시리즈 1 (검사량 - 파란색) -> 왼쪽 축 사용
            CombinedChartModel.Series.Add(new LineSeries
            {
                Title = "검사량",
                Color = OxyColor.Parse("#00C1D4"),
                StrokeThickness = 2,
                YAxisKey = "CountAxis"
            });

            // 4. 시리즈 2 (불량 개수 - 빨간색) -> ★ 수정: 왼쪽 축(CountAxis)을 같이 사용
            CombinedChartModel.Series.Add(new LineSeries
            {
                Title = "불량 개수",
                Color = OxyColor.Parse("#FF5252"),
                StrokeThickness = 2,
                YAxisKey = "CountAxis" // 원래 "DefectAxis"였던 것을 "CountAxis"로 변경
            });
        }

        // [추가된 기능] 키보드 입력 처리 (1: 불량, 2: 정상)
        public void ManualInput(bool isDefect)
        {
            CurrentComplete++;
            if (isDefect) DefectCount++;

            if (CurrentComplete > 0)
                DefectRate = (double)DefectCount / CurrentComplete * 100.0;
            else
                DefectRate = 0;

            UpdateChartData();
        }

        private void HandleImage1(byte[] data)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => CameraImage1 = ByteToBitmapImage(data));
        }

        private void HandleImage2(byte[] data)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => CameraImage2 = ByteToBitmapImage(data));
        }

        private BitmapImage? ByteToBitmapImage(byte[] data)
        {
            try
            {
                var image = new BitmapImage();
                using (var ms = new MemoryStream(data))
                {
                    ms.Position = 0;
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                }
                image.Freeze();
                return image;
            }
            catch { return null; }
        }

        private byte[]? ImageToByte(ImageSource? img)
        {
            if (img is WriteableBitmap wb)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(wb));
                    encoder.Save(ms);
                    return ms.ToArray();
                }
            }
            return null;
        }

        private void UpdateChartData()
        {
            DateTime now = DateTime.Now;
            if (CombinedChartModel.Series[0] is LineSeries countSeries)
            {
                countSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), CurrentComplete));
                if (countSeries.Points.Count > 50) countSeries.Points.RemoveAt(0);
            }
            if (CombinedChartModel.Series[1] is LineSeries defectSeries)
            {
                defectSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), DefectCount));
                if (defectSeries.Points.Count > 50) defectSeries.Points.RemoveAt(0);
            }
            CombinedChartModel.InvalidatePlot(true);
        }

        private void OnMqttDataReceived(string jsonPayload)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    dynamic data = JsonConvert.DeserializeObject(jsonPayload);
                    if (data == null) return;

                    int pid = (data.pid != null) ? (int)data.pid : 0;
                    string resultStr = (string)data.result;
                    bool isDefect = (resultStr == "NG" || resultStr == "DEFECTIVE");
                    string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    byte[]? img1Data = ImageToByte(CameraImage1);
                    byte[]? img2Data = ImageToByte(CameraImage2);

                    _dbManager.InsertMeasurement(pid, nowTime, isDefect, img1Data, img2Data);

                    CurrentComplete++;
                    if (isDefect) DefectCount++;
                    if (CurrentComplete > 0) DefectRate = (double)DefectCount / CurrentComplete * 100.0;

                    UpdateChartData();
                }
                catch { }
            });
        }

        public async void StartSimulation()
        {
            if (!_timer.IsEnabled)
            {
                bool success = await _apiService.StartSystemAsync("1");

                if (success)
                {
                    await _apiService.ControlCctvAsync("1");

                    string fastApiIp = "192.168.0.7";
                    int fastApiPort = 8000;
                    string url1 = $"ws://{fastApiIp}:{fastApiPort}/api/view/1";
                    string url2 = $"ws://{fastApiIp}:{fastApiPort}/api/view/2";

                    await _wsService1.ConnectAsync(url1);
                    await _wsService2.ConnectAsync(url2);

                    _timer.Start();
                    Timer_Tick(null, EventArgs.Empty);
                }
                else
                {
                    System.Windows.MessageBox.Show("시스템 시작 실패 (서버 응답 없음)");
                }
            }
        }

        public async void RestartSimulation()
        {
            bool success = await _apiService.RestartSystemAsync("1");

            if (success)
            {
                string fastApiIp = "192.168.0.7";
                int fastApiPort = 8000;
                await _wsService1.ConnectAsync($"ws://{fastApiIp}:{fastApiPort}/api/view/1");
                await _wsService2.ConnectAsync($"ws://{fastApiIp}:{fastApiPort}/api/view/2");

                CurrentComplete = 0;
                DefectCount = 0;
                DefectRate = 0;

                if (!_timer.IsEnabled)
                {
                    _timer.Start();
                    Timer_Tick(null, EventArgs.Empty);
                }
            }
        }

        public async void StopSimulation()
        {
            if (_timer.IsEnabled)
            {
                await _apiService.StopSystemAsync("1");
                await _apiService.ControlCctvAsync("0");

                await _wsService1.DisconnectAsync();
                await _wsService2.DisconnectAsync();

                CameraImage1 = null;
                CameraImage2 = null;

                _timer.Stop();
            }
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            var status = await _apiService.GetStatusAsync();
            if (status != null)
            {
                // 필요 시 서버 상태 동기화
            }
            UpdateChartData();
        }
    }
}