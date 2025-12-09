using AMPManager.Model;
using AMPManager.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging; // 이미지 처리를 위해 추가
using System.Windows.Shapes;
using System.IO; // MemoryStream을 위해 추가
using Newtonsoft.Json.Linq;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;

// 모호함 방지 (LiveCharts와 System.Windows.Media 충돌 방지)
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace AMPManager.View
{
    public partial class LogDetailWindow : Window
    {
        // ★ DB 매니저 대신 API 서비스를 사용합니다.
        private ApiService _apiService = new ApiService();
        private LogEntry _currentLog;

        public LogDetailWindow(LogEntry summaryLog)
        {
            InitializeComponent();
            _currentLog = summaryLog;

            // 1. 요약 정보로 먼저 화면 초기화
            SetStatusColor(_currentLog);
            this.DataContext = _currentLog;

            // 2. 창이 로드되면 서버에서 상세 데이터와 이미지를 가져옵니다 (비동기)
            this.Loaded += LogDetailWindow_Loaded;
        }

        private async void LogDetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // [A] 상세 데이터 가져오기 (/api/logsdetail?mid=...)
                var fullLog = await _apiService.GetLogDetailAsync(_currentLog.Id);

                if (fullLog != null)
                {
                    // 받아온 데이터로 현재 로그 객체 업데이트
                    _currentLog.MeasuredContour = fullLog.MeasuredContour;
                    _currentLog.MeasuredCenter = fullLog.MeasuredCenter;
                    _currentLog.TemplateData = fullLog.TemplateData;
                    _currentLog.HoleOffset = fullLog.HoleOffset;
                    _currentLog.AreaSize = fullLog.AreaSize;
                    _currentLog.DefectReason = fullLog.DefectReason;
                    // 필요한 경우 공차 정보 등도 업데이트
                    // _currentLog.TolShape = fullLog.TolShape; 
                }

                // [B] 이미지 가져오기 (/api/logs/{mid}/images)
                // Base64 문자열을 받아서 이미지로 변환
                var (imgBytes1, imgBytes2) = await _apiService.GetLogImagesAsync(_currentLog.Id);

                if (imgBytes1 != null) _currentLog.Img1 = ByteToImage(imgBytes1);
                if (imgBytes2 != null) _currentLog.Img2 = ByteToImage(imgBytes2);

                // [C] 데이터 갱신 후 상태 색상 재설정 및 그래프 그리기
                SetStatusColor(_currentLog);
                InitializeGraphs(_currentLog);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading details: {ex.Message}");
            }
        }

        // Base64 바이트 배열을 BitmapImage로 변환하는 함수
        private ImageSource ByteToImage(byte[] bytes)
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
                image.Freeze(); // UI 스레드 접근 허용
                return image;
            }
            catch { return null; }
        }

        private void SetStatusColor(LogEntry log)
        {
            // 정상(OK, Pass)이면 초록색, 아니면 빨간색
            bool isPass = (log.Status == "OK" || log.Status == "정상" || (log.Status != null && log.Status.Contains("Pass")));
            log.StatusColor = isPass ? Brushes.LightGreen : Brushes.Red;
        }

        private void InitializeGraphs(LogEntry log)
        {
            var measuredPoints = new ChartValues<ObservablePoint>();
            var idealPoints = new ChartValues<ObservablePoint>();

            double holeCx = 0, holeCy = 0;
            bool holeFound = false;

            // -------------------------------------------------------
            // [A] 데이터 파싱
            // -------------------------------------------------------

            // 1. 측정 형상 (Measured Contour) -> 파란색 실선용
            if (!string.IsNullOrWhiteSpace(log.MeasuredContour))
            {
                try
                {
                    var json = JObject.Parse(log.MeasuredContour);
                    var xArr = json["x"]?.ToObject<List<double>>();
                    var yArr = json["y"]?.ToObject<List<double>>();

                    if (xArr != null && yArr != null)
                    {
                        for (int i = 0; i < xArr.Count; i++)
                            measuredPoints.Add(new ObservablePoint(xArr[i], yArr[i]));

                        // 도형 닫기
                        if (measuredPoints.Count > 0)
                            measuredPoints.Add(new ObservablePoint(measuredPoints[0].X, measuredPoints[0].Y));
                    }
                }
                catch { }
            }

            // 2. 구멍 중심 (Measured Center)
            if (!string.IsNullOrWhiteSpace(log.MeasuredCenter))
            {
                try
                {
                    var json = JObject.Parse(log.MeasuredCenter);
                    holeFound = json["hole_found"]?.ToObject<bool>() ?? false;
                    if (holeFound)
                    {
                        holeCx = json["hole_cx"]?.ToObject<double>() ?? 0;
                        holeCy = json["hole_cy"]?.ToObject<double>() ?? 0;
                    }
                }
                catch { }
            }

            // 3. 기준 형상 (Template Data) -> 초록색 공차 영역용
            if (!string.IsNullOrWhiteSpace(log.TemplateData))
            {
                try
                {
                    var json = JObject.Parse(log.TemplateData);
                    var tx = json["x"]?.ToObject<List<double>>();
                    var ty = json["y"]?.ToObject<List<double>>();
                    if (tx != null && ty != null)
                    {
                        for (int i = 0; i < tx.Count; i++)
                            idealPoints.Add(new ObservablePoint(tx[i], ty[i]));
                    }
                }
                catch { }
            }

            // ★ 중요: 기준 데이터가 없으면 '기본 육각형'을 생성하여 배경으로 깝니다.
            if (idealPoints.Count == 0)
            {
                idealPoints.AddRange(new[] {
                    new ObservablePoint(0, 63), new ObservablePoint(55, 33), new ObservablePoint(55, -33),
                    new ObservablePoint(0, -63), new ObservablePoint(-55, -33), new ObservablePoint(-55, 33)
                });
            }
            // 기준 도형 닫기
            if (idealPoints.Count > 0 && (idealPoints[0].X != idealPoints.Last().X))
                idealPoints.Add(new ObservablePoint(idealPoints[0].X, idealPoints[0].Y));


            // -------------------------------------------------------
            // [B] 그래프 1: 형상 오버레이 (Shape)
            // -------------------------------------------------------
            log.ShapeVisuals = new VisualElementsCollection();

            // P0 ~ P5 라벨 (기준 도형 위치에 표시)
            for (int i = 0; i < idealPoints.Count && i < 6; i++)
            {
                var point = idealPoints[i];
                log.ShapeVisuals.Add(new VisualElement
                {
                    X = point.X * 1.1,
                    Y = point.Y * 1.1,
                    UIElement = new TextBlock
                    {
                        Text = $"P{i}",
                        Foreground = Brushes.White,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold
                    }
                });
            }
            // 중앙 점
            log.ShapeVisuals.Add(new VisualElement
            {
                X = 0,
                Y = 0,
                UIElement = new Ellipse { Width = 6, Height = 6, Fill = Brushes.White }
            });

            log.ShapeSeriesCollection = new SeriesCollection
            {
                // 1. 기준 영역 (초록색 띠 = 공차 범위) -> 이것이 '판별 기준'이 됩니다.
                new LineSeries
                {
                    Title = "Tolerance",
                    Values = idealPoints,
                    PointGeometry = null,
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)),
                    StrokeThickness = log.TolShape > 0 ? log.TolShape * 2 : 10, // 공차값 없으면 기본 두께 10
                    Fill = Brushes.Transparent
                },
                // 2. 기준선 (회색 점선 = 이상적인 모양)
                new LineSeries
                {
                    Title = "Ideal",
                    Values = idealPoints,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 5,
                    Stroke = Brushes.Gray,
                    StrokeDashArray = new DoubleCollection{ 2 },
                    Fill = Brushes.Transparent
                },
                // 3. 실제 측정값 (파란 실선) -> 기준 위에 덮어 그려짐
                new LineSeries
                {
                    Title = "Measured",
                    Values = measuredPoints,
                    PointGeometry = null,
                    Stroke = Brushes.DodgerBlue,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 30, 144, 255))
                }
            };


            // -------------------------------------------------------
            // [C] 그래프 2: 편차 (Deviation)
            // -------------------------------------------------------
            var deviations = new ChartValues<double>();
            var labels = new List<string>();

            if (measuredPoints.Count > 0)
            {
                int count = Math.Min(measuredPoints.Count, 60);
                for (int i = 0; i < count; i++)
                {
                    deviations.Add(measuredPoints[i].Y % 5); // 임시 시각화 데이터
                    labels.Add(i.ToString());
                }
            }
            else { deviations.Add(0); labels.Add("-"); }

            log.DeviationLabels = labels.ToArray();
            log.DeviationSeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Dev",
                    Values = deviations,
                    PointGeometry = null,
                    Stroke = Brushes.DodgerBlue,
                    Fill = new SolidColorBrush(Color.FromArgb(50, 30, 144, 255))
                }
            };
            // 편차 배경색 (정상/경고/불량)
            log.DeviationSections = new SectionsCollection
            {
                new AxisSection { Value = 0, SectionWidth = log.LimitWarn, Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 0)) },
                new AxisSection { Value = log.LimitWarn, SectionWidth = (log.LimitFail - log.LimitWarn), Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 0)) },
                new AxisSection { Value = log.LimitFail, SectionWidth = 10, Fill = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0)) }
            };


            // -------------------------------------------------------
            // [D] 그래프 3: 동심도 (Concentricity)
            // -------------------------------------------------------
            log.ConcentricitySeriesCollection = new SeriesCollection
            {
                // 중심 십자선
                new ScatterSeries
                {
                    Title = "Body Center",
                    Values = new ChartValues<ObservablePoint>{ new ObservablePoint(0,0) },
                    PointGeometry = DefaultGeometries.Cross,
                    MinPointShapeDiameter = 15,
                    Stroke = Brushes.White,
                    Fill = Brushes.Transparent
                },
                // 허용 범위 (초록색 점선 원) -> 기준
                new LineSeries
                {
                    Title = "Safe Zone",
                    Values = GetCircle(log.TolHole > 0 ? log.TolHole : 5.0),
                    PointGeometry = null,
                    Stroke = Brushes.Green,
                    StrokeDashArray = new DoubleCollection{ 2 },
                    Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0))
                }
            };

            if (holeFound)
            {
                double dist = Math.Sqrt(holeCx * holeCx + holeCy * holeCy);
                // 허용 범위를 넘으면 빨간색, 안이면 파란색
                var hColor = (log.TolHole > 0 && dist > log.TolHole) ? Brushes.Red : Brushes.DodgerBlue;

                log.ConcentricitySeriesCollection.Add(new ScatterSeries
                {
                    Title = "Hole Center",
                    Values = new ChartValues<ObservablePoint> { new ObservablePoint(holeCx, holeCy) },
                    PointGeometry = DefaultGeometries.Circle,
                    MinPointShapeDiameter = 10,
                    Fill = hColor
                });

                log.ConcentricitySeriesCollection.Add(new LineSeries
                {
                    Values = new ChartValues<ObservablePoint> { new ObservablePoint(0, 0), new ObservablePoint(holeCx, holeCy) },
                    PointGeometry = null,
                    Stroke = hColor,
                    StrokeThickness = 2
                });
            }

            // [E] 하단 히스토리 차트
            log.ChartSeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Hole Offset",
                    Values = new ChartValues<double> { log.HoleOffset },
                    PointGeometrySize = 10,
                    Stroke = Brushes.Orange
                }
            };
            log.ChartLabels = new[] { "Current" };
            log.YFormatter = value => value.ToString("F3");
        }

        private ChartValues<ObservablePoint> GetCircle(double r)
        {
            var p = new ChartValues<ObservablePoint>();
            for (int i = 0; i <= 360; i += 10)
            {
                double rad = i * Math.PI / 180;
                p.Add(new ObservablePoint(r * Math.Cos(rad), r * Math.Sin(rad)));
            }
            return p;
        }
    }
}