using AMPManager.Model;
using AMPManager.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using Newtonsoft.Json.Linq;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;

// 모호함 방지
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace AMPManager.View
{
    public partial class LogDetailWindow : Window
    {
        private ApiService _apiService = new ApiService();
        private LogEntry _currentLog;

        public LogDetailWindow(LogEntry summaryLog)
        {
            InitializeComponent();
            _currentLog = summaryLog;

            SetStatusColor(_currentLog);

            // 그래프 초기화 (수정된 디자인 적용)
            InitializeGraphs(_currentLog);

            this.DataContext = _currentLog;
            this.Loaded += LogDetailWindow_Loaded;
        }

        private async void LogDetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var fullLog = await _apiService.GetLogDetailAsync(_currentLog.Id);
                if (fullLog != null)
                {
                    _currentLog.MeasuredContour = fullLog.MeasuredContour;
                    _currentLog.MeasuredCenter = fullLog.MeasuredCenter;
                    _currentLog.TemplateData = fullLog.TemplateData;
                    _currentLog.HoleOffset = fullLog.HoleOffset;
                    _currentLog.AreaSize = fullLog.AreaSize;
                    _currentLog.DefectReason = fullLog.DefectReason;
                }

                var (imgBytes1, imgBytes2) = await _apiService.GetLogImagesAsync(_currentLog.Id);
                if (imgBytes1 != null) _currentLog.Img1 = ByteToImage(imgBytes1);
                if (imgBytes2 != null) _currentLog.Img2 = ByteToImage(imgBytes2);

                SetStatusColor(_currentLog);
                InitializeGraphs(_currentLog);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading details: {ex.Message}");
            }
        }

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
                image.Freeze();
                return image;
            }
            catch { return null; }
        }

        private void SetStatusColor(LogEntry log)
        {
            bool isPass = (log.Status == "OK" || log.Status == "정상" || (log.Status != null && log.Status.Contains("Pass")));
            log.StatusColor = isPass ? Brushes.LightGreen : Brushes.Red;
        }

        private void InitializeGraphs(LogEntry log)
        {
            var measuredPoints = new ChartValues<ObservablePoint>();
            var idealPoints = new ChartValues<ObservablePoint>();

            double holeCx = 0, holeCy = 0;
            bool holeFound = false;

            // [A] 데이터 파싱
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
                        if (measuredPoints.Count > 0)
                            measuredPoints.Add(new ObservablePoint(measuredPoints[0].X, measuredPoints[0].Y));
                    }
                }
                catch { }
            }

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

            // 기본 육각형 설정 (데이터 없을 시)
            if (idealPoints.Count == 0)
            {
                idealPoints.AddRange(new[] {
                    new ObservablePoint(0, 63), new ObservablePoint(55, 33), new ObservablePoint(55, -33),
                    new ObservablePoint(0, -63), new ObservablePoint(-55, -33), new ObservablePoint(-55, 33)
                });
            }
            if (idealPoints.Count > 0 && (idealPoints[0].X != idealPoints.Last().X))
                idealPoints.Add(new ObservablePoint(idealPoints[0].X, idealPoints[0].Y));


            // -------------------------------------------------------
            // [B] 그래프 1: 형상 분석 (Shape) - 각진 육각형(LineSmoothness=0) 적용
            // -------------------------------------------------------
            log.ShapeVisuals = new VisualElementsCollection();

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
            log.ShapeVisuals.Add(new VisualElement
            {
                X = 0,
                Y = 0,
                UIElement = new Ellipse { Width = 6, Height = 6, Fill = Brushes.White }
            });

            log.ShapeSeriesCollection = new SeriesCollection
            {
                // 1. Tolerance (배경): 각진 육각형 + 반투명 초록색 띠
                new LineSeries
                {
                    Title = "Tolerance",
                    Values = idealPoints,
                    PointGeometry = null,
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)),
                    StrokeThickness = log.TolShape > 0 ? log.TolShape * 2 : 10,
                    Fill = Brushes.Transparent,
                    LineSmoothness = 0 // ★ 각지게 그리기
                },
                // 2. Ideal (기준선): 각진 점선
                new LineSeries
                {
                    Title = "Ideal",
                    Values = idealPoints,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 5,
                    Stroke = Brushes.Gray,
                    StrokeDashArray = new DoubleCollection{ 2 },
                    Fill = Brushes.Transparent,
                    LineSmoothness = 0 // ★ 각지게 그리기
                },
                // 3. Measured (실측): 파란색 실선 + 각지게
                new LineSeries
                {
                    Title = "Measured",
                    Values = measuredPoints,
                    PointGeometry = null,
                    Stroke = Brushes.DodgerBlue,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 30, 144, 255)),
                    LineSmoothness = 0 // ★ 각지게 그리기 (측정 데이터도 포인트별 직선 연결)
                }
            };


            // -------------------------------------------------------
            // [C] 그래프 2: 편차 프로파일 (Deviation)
            // -------------------------------------------------------
            var deviations = new ChartValues<double>();
            var labels = new List<string>();

            if (measuredPoints.Count > 0)
            {
                int count = Math.Min(measuredPoints.Count, 60);
                for (int i = 0; i < count; i++)
                {
                    deviations.Add(measuredPoints[i].Y % 5);
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

            // 편차 배경색 (세이프 존 색상을 Tolerance와 통일)
            log.DeviationSections = new SectionsCollection
            {
                // Safe Zone: 반투명 초록색 (Shape 그래프와 통일감)
                new AxisSection
                {
                    Value = 0,
                    SectionWidth = log.LimitWarn,
                    Fill = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)) // ★ 수정됨
                },
                // Warn Zone: Yellow
                new AxisSection
                {
                    Value = log.LimitWarn,
                    SectionWidth = (log.LimitFail - log.LimitWarn),
                    Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 0))
                },
                // Fail Zone: Red
                new AxisSection
                {
                    Value = log.LimitFail,
                    SectionWidth = 10,
                    Fill = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0))
                }
            };


            // -------------------------------------------------------
            // [D] 그래프 3: 동심도 (Concentricity) - 빨간색 적용
            // -------------------------------------------------------
            log.ConcentricitySeriesCollection = new SeriesCollection
            {
                // 1. 중심 십자선
                new ScatterSeries
                {
                    Title = "Body Center",
                    Values = new ChartValues<ObservablePoint>{ new ObservablePoint(0,0) },
                    PointGeometry = DefaultGeometries.Cross,
                    MinPointShapeDiameter = 15,
                    Stroke = Brushes.White,
                    Fill = Brushes.Transparent,
                    StrokeThickness = 2
                },
                // 2. Safe Zone (초록색 점선 원)
                new LineSeries
                {
                    Title = "Safe Zone",
                    Values = GetCircle(log.TolHole > 0 ? log.TolHole : 5.0),
                    PointGeometry = null,
                    Stroke = Brushes.LimeGreen,
                    StrokeDashArray = new DoubleCollection{ 4, 2 },
                    Fill = new SolidColorBrush(Color.FromArgb(30, 50, 205, 50)),
                    StrokeThickness = 2
                }
            };

            if (holeFound)
            {
                // 3. Hole Center (항상 빨간색 점)
                log.ConcentricitySeriesCollection.Add(new ScatterSeries
                {
                    Title = "Hole Center",
                    Values = new ChartValues<ObservablePoint> { new ObservablePoint(holeCx, holeCy) },
                    PointGeometry = DefaultGeometries.Circle,
                    MinPointShapeDiameter = 12,
                    Fill = Brushes.Red,   // ★ 항상 빨간색
                    Stroke = Brushes.White,
                    StrokeThickness = 1
                });

                // 4. Offset Vector (항상 빨간색 선)
                log.ConcentricitySeriesCollection.Add(new LineSeries
                {
                    Title = "Offset Vector",
                    Values = new ChartValues<ObservablePoint>
                    {
                        new ObservablePoint(0, 0),
                        new ObservablePoint(holeCx, holeCy)
                    },
                    PointGeometry = null,
                    Stroke = Brushes.Red, // ★ 항상 빨간색
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
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
                    Stroke = Brushes.Orange,
                    Fill = Brushes.Transparent
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