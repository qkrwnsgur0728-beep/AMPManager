using AMPManager.Model;
using AMPManager.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Newtonsoft.Json.Linq;

// ★ 충돌 방지 별칭
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfDoubleCollection = System.Windows.Media.DoubleCollection;

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
                        for (int i = 0; i < xArr.Count; i++) measuredPoints.Add(new ObservablePoint(xArr[i], yArr[i]));
                        if (measuredPoints.Count > 0) measuredPoints.Add(new ObservablePoint(measuredPoints[0].X, measuredPoints[0].Y));
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

            // ★ DB의 정상 좌표 사용 (없으면 기본값)
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
                        if (idealPoints.Count > 0) idealPoints.Add(new ObservablePoint(idealPoints[0].X, idealPoints[0].Y));
                    }
                }
                catch { }
            }

            if (idealPoints.Count == 0) // 기본 육각형
            {
                idealPoints.AddRange(new[] {
                    new ObservablePoint(0, 63), new ObservablePoint(55, 33), new ObservablePoint(55, -33),
                    new ObservablePoint(0, -63), new ObservablePoint(-55, -33), new ObservablePoint(-55, 33),
                    new ObservablePoint(0, 63)
                });
            }

            // [Graph 1] 형상 (직선 연결)
            log.ShapeSeriesCollection = new SeriesCollection {
                new LineSeries {
                    Title = "Tolerance", Values = idealPoints, PointGeometry = null,
                    Stroke = new WpfSolidColorBrush(WpfColor.FromArgb(80, 0, 255, 0)),
                    StrokeThickness = log.TolShape * 2, Fill = WpfBrushes.Transparent,
                    LineSmoothness = 0 // ★ 직선
                },
                new LineSeries {
                    Title = "Ideal", Values = idealPoints, PointGeometry = DefaultGeometries.Circle, PointGeometrySize = 6,
                    Stroke = WpfBrushes.Gray, StrokeDashArray = new WpfDoubleCollection{2}, Fill = WpfBrushes.Transparent,
                    LineSmoothness = 0 // ★ 직선
                },
                new LineSeries {
                    Title = "Measured", Values = measuredPoints, PointGeometry = null,
                    Stroke = WpfBrushes.DodgerBlue, StrokeThickness = 2,
                    Fill = new WpfSolidColorBrush(WpfColor.FromArgb(30, 30, 144, 255)),
                    LineSmoothness = 0 // ★ 직선
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
                    labels.Add("P" + i);
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

            // [Graph 3] 동심도
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