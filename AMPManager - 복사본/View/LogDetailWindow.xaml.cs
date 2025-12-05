using AMPManager.Model;
using System.Windows;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AMPManager.View
{
    // LogDetailWindow.xaml.cs 파일에 이 코드를 사용하세요.
    public partial class LogDetailWindow : Window
    {
        public LogDetailWindow(LogEntry log)
        {
            InitializeComponent();

            // 1. 상태 색상 설정 (System.Windows.Media.Brushes 명시)
            log.StatusColor = log.Status == "불량"
                ? System.Windows.Media.Brushes.Red
                : System.Windows.Media.Brushes.MintCream;

            // 2. 그래프 데이터 초기화 및 기준선 설정 (HoleOffset 기준)

            // 💡 임시 데이터: 실제 DB에서 과거 HoleOffset 측정값들을 조회해야 합니다.
            var historicalHoleOffsets = new List<double> { 0.01, 0.05, 0.03, 0.08, log.HoleOffset };
            var historicalLabels = Enumerable.Range(1, historicalHoleOffsets.Count).Select(i => $"#{i}").ToArray();

            // 그래프 시리즈 구성
            log.ChartSeriesCollection = new SeriesCollection
            {
                // 1) 실제 측정값
                new LineSeries
                {
                    Title = "Hole Offset (mm)",
                    Values = new ChartValues<double>(historicalHoleOffsets),
                    PointGeometrySize = 10,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                },
                // 2) 경고 기준선 (Product.LimitWarn)
                new LineSeries
                {
                    Title = $"Warn Limit ({log.LimitWarn:F2})",
                    Values = new ChartValues<double>(Enumerable.Repeat(log.LimitWarn, historicalHoleOffsets.Count)),
                    PointGeometry = null,
                    Stroke = Brushes.Yellow,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Fill = Brushes.Transparent
                },
                // 3) 불합격 기준선 (Product.LimitFail)
                new LineSeries
                {
                    Title = $"Fail Limit ({log.LimitFail:F2})",
                    Values = new ChartValues<double>(Enumerable.Repeat(log.LimitFail, historicalHoleOffsets.Count)),
                    PointGeometry = null,
                    Stroke = Brushes.Red,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Fill = Brushes.Transparent
                }
            };

            log.ChartLabels = historicalLabels;
            log.YFormatter = value => value.ToString("F3") + " mm";

            this.DataContext = log;
        }
    }
}