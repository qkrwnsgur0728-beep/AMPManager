using AMPManager.Model;
using AMPManager.Core;
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
        private DatabaseManager _dbManager = new DatabaseManager();

        public LogDetailWindow(LogEntry summaryLog)
        {
            InitializeComponent();

            // 1. 상세 데이터 가져오기
            LogEntry fullLog = _dbManager.GetLogDetail(summaryLog.MeasureId);
            if (fullLog == null) fullLog = summaryLog;

            // 2. [중요] 가이드가 안 보이는 문제 해결 -> 공차 값이 0이면 기본값 강제 할당
            if (fullLog.LimitFail == 0) fullLog.LimitFail = 6.0;  // 편차 그래프 빨간 점선
            if (fullLog.LimitWarn == 0) fullLog.LimitWarn = 4.0;  // 편차 그래프 노란 배경 기준
            if (fullLog.TolShape == 0) fullLog.TolShape = 10.0;   // 형상 그래프 녹색 띠 두께
            if (fullLog.TolHole == 0) fullLog.TolHole = 5.0;      // 동심도 녹색 원 반지름

            // 3. 판정 결과 색상
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

            // =========================================================
            // [1] 데이터 파싱 (형상, 중심점, 기준데이터)
            // =========================================================
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
                        // 도형 닫기
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

            // DB 데이터 없을 때 보여줄 기본 육각형 (테스트용)
            if (idealPoints.Count == 0)
            {
                idealPoints.AddRange(new[] {
                    new ObservablePoint(0, 63), new ObservablePoint(55, 33), new ObservablePoint(55, -33),
                    new ObservablePoint(0, -63), new ObservablePoint(-55, -33), new ObservablePoint(-55, 33)
                });
            }
            if (idealPoints.Count > 0) idealPoints.Add(new ObservablePoint(idealPoints[0].X, idealPoints[0].Y));


            // =========================================================
            // [2] 그래프 1: 형상 분석 (Shape) - Overlay 적용
            // =========================================================
            log.ShapeVisuals = new VisualElementsCollection();

            // P0~P5 라벨 붙이기
            for (int i = 0; i < idealPoints.Count - 1 && i < 6; i++)
            {
                var point = idealPoints[i];
                double offsetX = point.X * 0.15; // 라벨을 조금 더 바깥으로
                double offsetY = point.Y * 0.15;
                if (Math.Abs(point.X) < 1) offsetX = (point.Y > 0) ? -15 : -15; // 상하단 포인트 위치 보정

                log.ShapeVisuals.Add(new VisualElement
                {
                    X = point.X + offsetX,
                    Y = point.Y + offsetY,
                    UIElement = new TextBlock { Text = $"P{i}", Foreground = WpfBrushes.White, FontWeight = FontWeights.Bold, FontSize = 14 }
                });
            }
            // 중앙 십자 마크
            log.ShapeVisuals.Add(new VisualElement
            {
                X = 0,
                Y = 0,
                UIElement = new Path
                {
                    Data = Geometry.Parse("M -5,0 L 5,0 M 0,-5 L 0,5"),
                    Stroke = WpfBrushes.Cyan,
                    StrokeThickness = 2
                }
            });

            log.ShapeSeriesCollection = new SeriesCollection
            {
                // 1) [가이드] 허용 공차 띠 (반투명 녹색 굵은 선)
                // StrokeThickness를 TolShape(공차) * 2로 설정해 띠처럼 보이게 함
                new LineSeries
                {
                    Title = "Tolerance Band",
                    Values = idealPoints,
                    PointGeometry = null,
                    Stroke = new WpfSolidColorBrush(WpfColor.FromArgb(60, 0, 255, 0)), // 60: 투명도 (흐릿한 녹색)
                    StrokeThickness = log.TolShape * 2, // ★ 핵심: 이 두께 안에 파란선이 들어와야 함
                    Fill = WpfBrushes.Transparent,
                    LineSmoothness = 0
                },
                // 2) [기준] Ideal 형상 (회색 점선)
                new LineSeries
                {
                    Title = "Ideal",
                    Values = idealPoints,
                    PointGeometry = DefaultGeometries.Circle, PointGeometrySize = 5,
                    Stroke = WpfBrushes.Gray,
                    StrokeDashArray = new WpfDoubleCollection { 2 }, // 점선
                    Fill = WpfBrushes.Transparent,
                    LineSmoothness = 0
                },
                // 3) [측정] 실제 형상 (파란 실선) -> 제일 위에 그림
                new LineSeries
                {
                    Title = "Measured",
                    Values = measuredPoints,
                    PointGeometry = null,
                    Stroke = WpfBrushes.DodgerBlue,
                    StrokeThickness = 2,
                    Fill = new WpfSolidColorBrush(WpfColor.FromArgb(30, 30, 144, 255)), // 내부 살짝 채움
                    LineSmoothness = 0
                },
                // 4) [기준] Ref Edge (회전 확인용 빨간선)
                new LineSeries
                {
                    Title = "Ref Edge",
                    Values = new ChartValues<ObservablePoint> { new ObservablePoint(0, 0), new ObservablePoint(idealPoints[0].X, idealPoints[0].Y) },
                    PointGeometry = null,
                    Stroke = WpfBrushes.Red,
                    StrokeThickness = 2,
                    Fill = WpfBrushes.Transparent
                }
            };


            // =========================================================
            // [3] 그래프 2: 편차 프로파일 (Deviation) - 배경 섹션 적용
            // =========================================================
            var deviations = new ChartValues<double>();
            var labels = new List<string>();

            if (measuredPoints.Count > 0)
            {
                double sumDist = measuredPoints.Take(measuredPoints.Count - 1).Sum(p => Math.Sqrt(p.X * p.X + p.Y * p.Y));
                double meanRadius = sumDist / (measuredPoints.Count - 1);

                for (int i = 0; i < measuredPoints.Count - 1; i++)
                {
                    double dist = Math.Sqrt(measuredPoints[i].X * measuredPoints[i].X + measuredPoints[i].Y * measuredPoints[i].Y);
                    deviations.Add(dist - meanRadius); // 평균 반경과의 차이
                    labels.Add("P" + (i % 6));
                }
                // 그래프 끝 연결
                if (deviations.Count > 0) deviations.Add(deviations[0]);
                labels.Add("P0");
            }
            else { deviations.Add(0); labels.Add("-"); }

            log.DeviationLabels = labels.ToArray();

            // ★ 가이드 배경 설정 (초록/노랑/빨강)
            log.DeviationSections = new SectionsCollection
            {
                // 안전 구역 (Green)
                new AxisSection { Value = -log.LimitWarn, SectionWidth = log.LimitWarn * 2, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(30, 0, 255, 0)) },
                // 경고 구역 (Yellow) - 위쪽
                new AxisSection { Value = log.LimitWarn, SectionWidth = log.LimitFail - log.LimitWarn, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(30, 255, 255, 0)) },
                // 경고 구역 (Yellow) - 아래쪽
                new AxisSection { Value = -log.LimitFail, SectionWidth = log.LimitFail - log.LimitWarn, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(30, 255, 255, 0)) },
                // 위험 구역 (Red)은 AxisSection 대신 Limit 선으로 표현하거나 추가 가능
            };

            log.DeviationSeriesCollection = new SeriesCollection
            {
                // 실제 편차 데이터
                new LineSeries
                {
                    Title = "Dev", Values = deviations,
                    PointGeometry = null, Stroke = WpfBrushes.Cyan, StrokeThickness = 2,
                    Fill = WpfBrushes.Transparent, LineSmoothness = 1
                },
                // 상한선 (빨간 점선)
                new LineSeries
                {
                    Title = "Max Limit",
                    Values = new ChartValues<double>(Enumerable.Repeat(log.LimitFail, deviations.Count)),
                    PointGeometry = null, Stroke = WpfBrushes.Red, StrokeThickness = 2, StrokeDashArray = new WpfDoubleCollection { 2 },
                    Fill = WpfBrushes.Transparent
                },
                // 하한선 (빨간 점선)
                new LineSeries
                {
                    Title = "Min Limit",
                    Values = new ChartValues<double>(Enumerable.Repeat(-log.LimitFail, deviations.Count)),
                    PointGeometry = null, Stroke = WpfBrushes.Red, StrokeThickness = 2, StrokeDashArray = new WpfDoubleCollection { 2 },
                    Fill = WpfBrushes.Transparent
                }
            };


            // =========================================================
            // [4] 그래프 3: 동심도 (Concentricity) - 조준선 및 Safe Circle
            // =========================================================
            var centerColor = (Math.Sqrt(holeCx * holeCx + holeCy * holeCy) <= log.TolHole) ? WpfBrushes.LimeGreen : WpfBrushes.Red;

            log.ConcentricitySeriesCollection = new SeriesCollection
            {
                // 1) 십자선 (기준점 0,0)
                new ScatterSeries
                {
                    Title = "Center", Values = new ChartValues<ObservablePoint> { new ObservablePoint(0, 0) },
                    PointGeometry = DefaultGeometries.Cross, MinPointShapeDiameter = 15,
                    Stroke = WpfBrushes.White, StrokeThickness = 2
                },
                // 2) [가이드] 안전 범위 (녹색 원)
                new LineSeries
                {
                    Title = "Safe Zone", Values = GetCircle(log.TolHole),
                    PointGeometry = null,
                    Stroke = WpfBrushes.LimeGreen, StrokeThickness = 2, StrokeDashArray = new WpfDoubleCollection { 2 },
                    Fill = new WpfSolidColorBrush(WpfColor.FromArgb(20, 0, 255, 0)) // 내부 살짝 칠하기
                }
            };

            // 3) [측정] 구멍 중심점 (빨강/초록 점)
            if (holeFound)
            {
                log.ConcentricitySeriesCollection.Add(new ScatterSeries
                {
                    Title = "Hole",
                    Values = new ChartValues<ObservablePoint> { new ObservablePoint(holeCx, holeCy) },
                    PointGeometry = DefaultGeometries.Circle,
                    MinPointShapeDiameter = 10,
                    Fill = centerColor
                });
                // 원점과 측정점 연결선
                log.ConcentricitySeriesCollection.Add(new LineSeries
                {
                    Values = new ChartValues<ObservablePoint> { new ObservablePoint(0, 0), new ObservablePoint(holeCx, holeCy) },
                    PointGeometry = null,
                    Stroke = centerColor,
                    StrokeThickness = 1,
                    StrokeDashArray = new WpfDoubleCollection { 2 }
                });
            }
        }

        // 원 그리기 헬퍼 함수
        private ChartValues<ObservablePoint> GetCircle(double r)
        {
            var p = new ChartValues<ObservablePoint>();
            // 점을 촘촘하게 찍어서 원처럼 보이게 함
            for (int i = 0; i <= 360; i += 10)
            {
                double rad = i * Math.PI / 180.0;
                p.Add(new ObservablePoint(r * Math.Cos(rad), r * Math.Sin(rad)));
            }
            return p;
        }
    }
}