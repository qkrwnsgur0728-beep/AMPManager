using AMPManager.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using Newtonsoft.Json.Linq;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfDoubleCollection = System.Windows.Media.DoubleCollection;
using System.Windows.Media;

namespace AMPManager.View
{
    public partial class LogDetailWindow : Window
    {
        // ★ DB 매니저 제거 (이제 서버에서 받은 데이터를 씀)

        public LogDetailWindow(LogEntry fullLog)
        {
            InitializeComponent();

            // ViewModel에서 이미 데이터를 꽉 채워서 보내줬으므로 바로 사용
            bool isPass = (fullLog.Status == "정상" || fullLog.Status == "OK" || (fullLog.Status != null && fullLog.Status.Contains("Pass")));
            fullLog.StatusColor = isPass ? WpfBrushes.LightGreen : WpfBrushes.Red;

            try
            {
                InitializeGraphs(fullLog);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Graph Error: {ex.Message}");
            }

            this.DataContext = fullLog;
        }

        private void InitializeGraphs(LogEntry log)
        {
            var measuredPoints = new ChartValues<ObservablePoint>();
            var idealPoints = new ChartValues<ObservablePoint>();

            double holeCx = 0, holeCy = 0;
            bool holeFound = false;

            // [A] 데이터 파싱 (Measured Contour)
            if (!string.IsNullOrWhiteSpace(log.MeasuredContour))
            {
                try
                {
                    var json = JObject.Parse(log.MeasuredContour);
                    var xArr = json["x"]?.ToObject<List<double>>();
                    var yArr = json["y"]?.ToObject<List<double>>();
                    if (xArr != null && yArr != null)
                    {
                        for (int i = 0; i < xArr.Count; i++) measuredPoints.Add(new ObservablePoint(xArr[i], yArr[i]));
                        if (measuredPoints.Count > 0) measuredPoints.Add(new ObservablePoint(measuredPoints[0].X, measuredPoints[0].Y));
                    }
                }
                catch { }
            }

            // [A] 데이터 파싱 (Center Info)
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

            // [A] 데이터 파싱 (TemplateData)
            if (!string.IsNullOrWhiteSpace(log.TemplateData))
            {
                try
                {
                    var json = JObject.Parse(log.TemplateData);
                    var tx = json["x"]?.ToObject<List<double>>();
                    var ty = json["y"]?.ToObject<List<double>>();
                    if (tx != null && ty != null)
                    {
                        for (int i = 0; i < tx.Count; i++) idealPoints.Add(new ObservablePoint(tx[i], ty[i]));
                    }
                }
                catch { }
            }

            // 기본 도형 (데이터 없을 시)
            if (idealPoints.Count == 0)
            {
                idealPoints.AddRange(new[] {
                    new ObservablePoint(0, 63), new ObservablePoint(55, 33), new ObservablePoint(55, -33),
                    new ObservablePoint(0, -63), new ObservablePoint(-55, -33), new ObservablePoint(-55, 33)
                });
            }

            // 라벨 및 비주얼 요소
            log.ShapeVisuals = new VisualElementsCollection();
            for (int i = 0; i < idealPoints.Count && i < 6; i++)
            {
                var point = idealPoints[i];
                double offsetX = point.X * 0.1;
                double offsetY = point.Y * 0.1;
                if (Math.Abs(point.X) < 1) offsetX = (point.Y > 0) ? -20 : -20;

                log.ShapeVisuals.Add(new VisualElement
                {
                    X = point.X + offsetX,
                    Y = point.Y + offsetY,
                    UIElement = new TextBlock { Text = $"P{i}", Foreground = WpfBrushes.White, FontSize = 14, FontWeight = FontWeights.Bold }
                });
            }
            log.ShapeVisuals.Add(new VisualElement
            {
                X = 0,
                Y = 0,
                UIElement = new Ellipse { Width = 8, Height = 8, Fill = WpfBrushes.White }
            });

            if (idealPoints.Count > 0) idealPoints.Add(new ObservablePoint(idealPoints[0].X, idealPoints[0].Y));

            // [Graph 1] 형상 (육각형 가이드)
            log.ShapeSeriesCollection = new SeriesCollection {
                // (1) 초록색 육각형 가이드 (Tolerance) - 어두운 초록색 (G=190) 적용
                new LineSeries {
                    Title = "Tolerance",
                    Values = idealPoints,
                    PointGeometry = null,
                    Stroke = new WpfSolidColorBrush(WpfColor.FromArgb(200, 0, 190, 0)),
                    StrokeThickness = log.TolShape * 2,
                    Fill = WpfBrushes.Transparent,
                    LineSmoothness = 0
                },
                // (2) Ref Edge, Ideal (배경)
                new LineSeries { Title = "Ref Edge", Values = new ChartValues<ObservablePoint> { new ObservablePoint(0, 0), new ObservablePoint(idealPoints[0].X, idealPoints[0].Y) }, PointGeometry = null, Stroke = WpfBrushes.Red, StrokeThickness = 2, Fill = WpfBrushes.Transparent, LineSmoothness = 0 },
                new LineSeries { Title = "Ideal", Values = idealPoints, PointGeometry = DefaultGeometries.Circle, PointGeometrySize = 6, Stroke = WpfBrushes.Gray, StrokeDashArray = new WpfDoubleCollection{2}, Fill = WpfBrushes.Transparent, LineSmoothness = 0 },
                // (3) Measured (측정값) - 가장 위에 오버레이
                new LineSeries { Title = "Measured", Values = measuredPoints, PointGeometry = null, Stroke = WpfBrushes.Blue, StrokeThickness = 2, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(30, 30, 144, 255)), LineSmoothness = 0 }
            };

            // [Graph 2] 편차 (굵은 영역 가이드)
            var deviations = new ChartValues<double>();
            var labels = new List<string>();
            if (measuredPoints.Count > 0)
            {
                double sumDist = measuredPoints.Take(measuredPoints.Count - 1).Sum(p => Math.Sqrt(p.X * p.X + p.Y * p.Y));
                double meanRadius = (measuredPoints.Count > 1) ? sumDist / (measuredPoints.Count - 1) : 0;
                for (int i = 0; i < measuredPoints.Count - 1; i++)
                {
                    deviations.Add(Math.Sqrt(measuredPoints[i].X * measuredPoints[i].X + measuredPoints[i].Y * measuredPoints[i].Y) - meanRadius);
                    labels.Add("P" + (i % 6).ToString());
                }
                if (deviations.Count > 0) deviations.Add(deviations[0]);
                labels.Add("P0");
            }
            else { deviations.Add(0); labels.Add("-"); }

            log.DeviationLabels = labels.ToArray();
            log.DeviationSeriesCollection = new SeriesCollection {
                // (1) Measured (측정값) - 가장 위에 오버레이
                new LineSeries { Title="Dev", Values=deviations, PointGeometry=null, Stroke=WpfBrushes.Blue, StrokeThickness=2, Fill=WpfBrushes.Transparent, LineSmoothness=0 },
                // (2) Max/Min (빨간색 불량 기준선)
                new LineSeries { Title="Max", Values=new ChartValues<double>(Enumerable.Repeat(log.LimitFail, deviations.Count)), PointGeometry=null, Stroke=WpfBrushes.Red, StrokeDashArray=new WpfDoubleCollection{2}, Fill=WpfBrushes.Transparent },
                new LineSeries { Title="Min", Values=new ChartValues<double>(Enumerable.Repeat(-log.LimitFail, deviations.Count)), PointGeometry=null, Stroke=WpfBrushes.Red, StrokeDashArray=new WpfDoubleCollection{2}, Fill=WpfBrushes.Transparent }
            };
            // (3) 초록색/노란색 영역 가이드 (AxisSection) - 어두운 초록색 (G=190) 적용
            log.DeviationSections = new SectionsCollection {
                // 초록색 안전 영역
                new AxisSection { Value = -log.LimitWarn, SectionWidth = log.LimitWarn * 2, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(120, 0, 190, 0)) }, 
                // 노란색 경고 영역 (상단)
                new AxisSection { Value = log.LimitWarn, SectionWidth = log.LimitFail - log.LimitWarn, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(120, 255, 255, 0)) }, 
                // 노란색 경고 영역 (하단)
                new AxisSection { Value = -log.LimitFail, SectionWidth = log.LimitFail - log.LimitWarn, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(120, 255, 255, 0)) }
            };

            // [Graph 3] 동심도 (원 가이드)
            log.ConcentricitySeriesCollection = new SeriesCollection {
                // (1) 중앙 기준점 (Body)
                new ScatterSeries { Title="Body", Values=new ChartValues<ObservablePoint>{new ObservablePoint(0,0)}, PointGeometry=DefaultGeometries.Cross, MinPointShapeDiameter=20, Stroke=WpfBrushes.Black, StrokeThickness = 2, Fill = WpfBrushes.Transparent },
                // (2) 초록색 원형 가이드 (Safe Zone) - 어두운 초록색 (G=190) 적용
                new LineSeries {
                    Title = "Safe",
                    Values = GetCircle(log.TolHole),
                    PointGeometry = null,
                    Stroke = new WpfSolidColorBrush(WpfColor.FromRgb(0, 150, 0)), // 더 진한 녹색 선 (0, 150, 0)
                    StrokeDashArray = new WpfDoubleCollection{2},
                    Fill = new WpfSolidColorBrush(WpfColor.FromArgb(100, 0, 190, 0)) // 어두운 녹색 영역 채우기
                }
            };
            if (holeFound)
            {
                // (3) Measured (측정값) - 가장 위에 오버레이
                var hColor = (Math.Sqrt(holeCx * holeCx + holeCy * holeCy) <= log.TolHole) ? WpfBrushes.Blue : WpfBrushes.Red;
                log.ConcentricitySeriesCollection.Add(new ScatterSeries { Values = new ChartValues<ObservablePoint> { new ObservablePoint(holeCx, holeCy) }, PointGeometry = DefaultGeometries.Circle, MinPointShapeDiameter = 10, Fill = hColor });
                log.ConcentricitySeriesCollection.Add(new LineSeries { Values = new ChartValues<ObservablePoint> { new ObservablePoint(0, 0), new ObservablePoint(holeCx, holeCy) }, PointGeometry = null, Stroke = hColor, StrokeThickness = 2, Fill = WpfBrushes.Transparent });
            }
        }

        private ChartValues<ObservablePoint> GetCircle(double r)
        {
            var p = new ChartValues<ObservablePoint>();
            for (int i = 0; i <= 360; i += 5) p.Add(new ObservablePoint(r * Math.Cos(i * Math.PI / 180), r * Math.Sin(i * Math.PI / 180)));
            return p;
        }
    }
}