using AMPManager.Model;
using AMPManager.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls; // TextBlock 사용
using System.Windows.Shapes; // Ellipse 사용
using Newtonsoft.Json.Linq;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfDoubleCollection = System.Windows.Media.DoubleCollection;
using System.Windows.Media; // FontWeight 사용

namespace AMPManager.View
{
    public partial class LogDetailWindow : Window
    {
        private DatabaseManager _dbManager = new DatabaseManager();

        public LogDetailWindow(LogEntry summaryLog)
        {
            InitializeComponent();

            LogEntry fullLog = _dbManager.GetLogDetail(summaryLog.MeasureId);
            if (fullLog == null) fullLog = summaryLog;

            bool isPass = (fullLog.Status == "OK" || (fullLog.Status != null && fullLog.Status.Contains("Pass")));
            fullLog.StatusColor = isPass ? WpfBrushes.LightGreen : WpfBrushes.Red;

            try { InitializeGraphs(fullLog); }
            catch (Exception ex) { Console.WriteLine($"Graph Error: {ex.Message}"); }

            this.DataContext = fullLog;
        }

        private void InitializeGraphs(LogEntry log)
        {
            var measuredPoints = new ChartValues<ObservablePoint>();
            var idealPoints = new ChartValues<ObservablePoint>(); // Ideal points for the shape

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
                        // Measured points 닫기 (LineSeries 용)
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

            // [A] 데이터 파싱 (TemplateData: Ideal Points)
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

            // 기본 육각형 (DB 데이터 없을 경우)
            if (idealPoints.Count == 0)
            {
                // P0(상단) 부터 시계방향
                idealPoints.AddRange(new[] {
                    new ObservablePoint(0, 63),   // P0
                    new ObservablePoint(55, 33),  // P1
                    new ObservablePoint(55, -33), // P2
                    new ObservablePoint(0, -63),  // P3
                    new ObservablePoint(-55, -33),// P4
                    new ObservablePoint(-55, 33)  // P5
                });
            }

            // ------------------------------------------------------------------
            // ★★★ P0 ~ P5 라벨 및 중앙 점 추가 로직 (VisualElements) ★★★
            // ------------------------------------------------------------------
            log.ShapeVisuals = new VisualElementsCollection();

            // 1. 라벨 추가 (P0 ~ P5)
            for (int i = 0; i < idealPoints.Count && i < 6; i++)
            {
                var point = idealPoints[i];

                // 텍스트 위치 약간 바깥쪽으로 조정 (오프셋)
                double offsetX = point.X * 0.1;
                double offsetY = point.Y * 0.1;

                // P0, P3의 X축 오프셋 보정
                if (Math.Abs(point.X) < 1) offsetX = (point.Y > 0) ? -20 : -20;

                log.ShapeVisuals.Add(new VisualElement
                {
                    X = point.X + offsetX,
                    Y = point.Y + offsetY,
                    UIElement = new TextBlock
                    {
                        Text = $"P{i}",
                        Foreground = WpfBrushes.White,
                        FontSize = 14,
                        FontWeight = FontWeights.Bold
                    }
                });
            }

            // 2. 중앙 점 (파란색 Cross)
            log.ShapeVisuals.Add(new VisualElement
            {
                X = 0,
                Y = 0,
                UIElement = new System.Windows.Shapes.Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = WpfBrushes.White
                }
            });


            // 닫힌 도형을 위해 Ideal points에 마지막 점 추가 (LineSeries 용)
            if (idealPoints.Count > 0) idealPoints.Add(new ObservablePoint(idealPoints[0].X, idealPoints[0].Y));


            // [Graph 1] 형상 (직선 연결) - Ref Edge 라인 추가됨
            log.ShapeSeriesCollection = new SeriesCollection {
                // 1. Tolerance (공차 영역)
                new LineSeries {
                    Title = "Tolerance", Values = idealPoints, PointGeometry = null,
                    Stroke = new WpfSolidColorBrush(WpfColor.FromArgb(80, 0, 255, 0)),
                    StrokeThickness = log.TolShape * 2, Fill = WpfBrushes.Transparent,
                    LineSmoothness = 0
                },
                // 2. Ref Edge (중앙 -> P0 빨간 실선) ★ 추가됨
                new LineSeries {
                    Title = "Ref Edge",
                    Values = new ChartValues<ObservablePoint> { new ObservablePoint(0, 0), new ObservablePoint(idealPoints[0].X, idealPoints[0].Y) },
                    PointGeometry = null,
                    Stroke = WpfBrushes.Red,
                    StrokeThickness = 2,
                    Fill = WpfBrushes.Transparent,
                    LineSmoothness = 0
                },
                // 3. Ideal (회색 점선)
                new LineSeries {
                    Title = "Ideal", Values = idealPoints, PointGeometry = DefaultGeometries.Circle, PointGeometrySize = 6,
                    Stroke = WpfBrushes.Gray, StrokeDashArray = new WpfDoubleCollection{2}, Fill = WpfBrushes.Transparent,
                    LineSmoothness = 0
                },
                // 4. Measured (측정값 - 파란 실선)
                new LineSeries {
                    Title = "Measured", Values = measuredPoints, PointGeometry = null,
                    Stroke = WpfBrushes.DodgerBlue, StrokeThickness = 2,
                    Fill = new WpfSolidColorBrush(WpfColor.FromArgb(30, 30, 144, 255)),
                    LineSmoothness = 0
                }
            };

            // [Graph 2] 편차
            var deviations = new ChartValues<double>();
            var labels = new List<string>();
            if (measuredPoints.Count > 0)
            {
                double sumDist = measuredPoints.Take(measuredPoints.Count - 1).Sum(p => Math.Sqrt(p.X * p.X + p.Y * p.Y));
                double meanRadius = (measuredPoints.Count > 1) ? sumDist / (measuredPoints.Count - 1) : 0;
                for (int i = 0; i < measuredPoints.Count - 1; i++)
                {
                    deviations.Add(Math.Sqrt(measuredPoints[i].X * measuredPoints[i].X + measuredPoints[i].Y * measuredPoints[i].Y) - meanRadius);
                    // P0~P5 라벨링과 일치하도록 Deviation 그래프 라벨 수정
                    labels.Add("P" + (i % 6).ToString());
                }
                if (deviations.Count > 0) deviations.Add(deviations[0]);
                labels.Add("P0");
            }
            else { deviations.Add(0); labels.Add("-"); }

            log.DeviationLabels = labels.ToArray();
            log.DeviationSeriesCollection = new SeriesCollection {
                new LineSeries { Title="Dev", Values=deviations, PointGeometry=null, Stroke=WpfBrushes.Blue, StrokeThickness=2, Fill=WpfBrushes.Transparent, LineSmoothness=0 },
                new LineSeries { Title="Max", Values=new ChartValues<double>(Enumerable.Repeat(log.LimitFail, deviations.Count)), PointGeometry=null, Stroke=WpfBrushes.Red, StrokeDashArray=new WpfDoubleCollection{2}, Fill=WpfBrushes.Transparent },
                new LineSeries { Title="Min", Values=new ChartValues<double>(Enumerable.Repeat(-log.LimitFail, deviations.Count)), PointGeometry=null, Stroke=WpfBrushes.Red, StrokeDashArray=new WpfDoubleCollection{2}, Fill=WpfBrushes.Transparent }
            };
            log.DeviationSections = new SectionsCollection {
                new AxisSection { Value = -log.LimitWarn, SectionWidth = log.LimitWarn * 2, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(40, 0, 255, 0)) },
                new AxisSection { Value = log.LimitWarn, SectionWidth = log.LimitFail - log.LimitWarn, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(40, 255, 255, 0)) },
                new AxisSection { Value = -log.LimitFail, SectionWidth = log.LimitFail - log.LimitWarn, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(40, 255, 255, 0)) }
            };

            // [Graph 3] 동심도 (축 범위 -20 ~ 20)
            log.ConcentricitySeriesCollection = new SeriesCollection {
                new ScatterSeries { Title="Body", Values=new ChartValues<ObservablePoint>{new ObservablePoint(0,0)}, PointGeometry=DefaultGeometries.Cross, MinPointShapeDiameter=20, Stroke=WpfBrushes.Black, StrokeThickness=2, Fill=WpfBrushes.Transparent },
                new LineSeries { Title="Safe", Values=GetCircle(log.TolHole), PointGeometry=null, Stroke=WpfBrushes.Green, StrokeDashArray=new WpfDoubleCollection{2}, Fill=new WpfSolidColorBrush(WpfColor.FromArgb(30,0,255,0)) }
            };
            if (holeFound)
            {
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