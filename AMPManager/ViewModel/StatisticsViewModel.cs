using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using AMPManager.Core;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Legends;

namespace AMPManager.ViewModel
{
    public class StatisticsViewModel : BaseViewModel
    {
        // [변경] 로컬 DB 대신 API 서비스 사용
        private ApiService _apiService = new ApiService();

        // 1. 기간 선택 (기본값: 최근 7일)
        private DateTime _startDate = DateTime.Now.AddDays(-6);
        private DateTime _endDate = DateTime.Now;

        public DateTime StartDate
        {
            get => _startDate;
            set { SetProperty(ref _startDate, value); LoadChartData(); }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set { SetProperty(ref _endDate, value); LoadChartData(); }
        }

        public ICommand SearchCommand { get; }

        // 2. 그래프 모델
        private PlotModel _defectRateModel;
        public PlotModel DefectRateModel
        {
            get => _defectRateModel;
            set => SetProperty(ref _defectRateModel, value);
        }

        // 3. 통계 수치 (Master 버전처럼 '개수'로 변경)
        private string _cntShape = "0";
        private string _cntCenter = "0";
        private string _cntRust = "0";
        private string _cntTotal = "0";

        public string CntShape { get => _cntShape; set => SetProperty(ref _cntShape, value); }
        public string CntCenter { get => _cntCenter; set => SetProperty(ref _cntCenter, value); }
        public string CntRust { get => _cntRust; set => SetProperty(ref _cntRust, value); }
        public string CntTotal { get => _cntTotal; set => SetProperty(ref _cntTotal, value); }

        public StatisticsViewModel()
        {
            SearchCommand = new RelayCommand(o => LoadChartData());

            InitializeChart();
            LoadChartData();
        }

        private void InitializeChart()
        {
            var model = new PlotModel { Title = "" };
            var textColor = OxyColor.Parse("#E0E0E0");
            var gridColor = OxyColor.Parse("#4A4A5A");

            model.Background = OxyColors.Transparent;
            model.PlotAreaBorderColor = OxyColors.Transparent;
            model.TextColor = textColor;

            // [변경] 범례 추가 (검사량 vs 불량 수 구분을 위해)
            model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.TopRight,
                LegendTextColor = textColor,
                LegendBackground = OxyColors.Transparent,
                LegendBorder = OxyColors.Transparent
            });

            // X축 (날짜)
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "MM-dd",
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = gridColor,
                AxislineColor = gridColor,
                TicklineColor = gridColor,
                TextColor = textColor,
                IntervalType = DateTimeIntervalType.Days,
                MajorStep = 1.0
            });

            // Y축 (수량)
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "수량(개)",
                Minimum = 0,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = gridColor,
                AxislineColor = gridColor,
                TextColor = textColor
            });

            DefectRateModel = model;
        }

        private async void LoadChartData()
        {
            // [변경] API를 통해 데이터 수신
            var stats = await _apiService.GetStatisticsAsync(StartDate, EndDate);

            if (stats == null) return;

            // 1. 하단 카드 (개수) 갱신
            if (stats.counts != null)
            {
                CntShape = $"{stats.counts.shape} 개";
                CntCenter = $"{stats.counts.center} 개";
                CntRust = $"{stats.counts.rust} 개";
                CntTotal = $"{stats.counts.total_ng} 개";
            }

            // 2. 그래프 갱신
            if (DefectRateModel != null && stats.daily_data != null)
            {
                DefectRateModel.Series.Clear();

                // X축 범위 재설정 (선택한 기간에 맞춤)
                var dateAxis = DefectRateModel.Axes.FirstOrDefault(x => x.Position == AxisPosition.Bottom) as DateTimeAxis;
                if (dateAxis != null)
                {
                    dateAxis.Minimum = DateTimeAxis.ToDouble(StartDate);
                    dateAxis.Maximum = DateTimeAxis.ToDouble(EndDate);
                }

                // 라인 1: 전체 검사량 (파란색)
                var totalSeries = new LineSeries
                {
                    Title = "전체 검사",
                    Color = OxyColor.Parse("#00C1D4"), // Cyan
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 3,
                    StrokeThickness = 2
                };

                // 라인 2: 불량 수 (빨간색)
                var defectSeries = new LineSeries
                {
                    Title = "불량 수",
                    Color = OxyColor.Parse("#FF5252"), // Red
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 3,
                    StrokeThickness = 2
                };

                foreach (var item in stats.daily_data)
                {
                    if (DateTime.TryParse(item.date, out DateTime dt))
                    {
                        double xVal = DateTimeAxis.ToDouble(dt);
                        totalSeries.Points.Add(new DataPoint(xVal, item.total));
                        defectSeries.Points.Add(new DataPoint(xVal, item.defect));
                    }
                }

                DefectRateModel.Series.Add(totalSeries);
                DefectRateModel.Series.Add(defectSeries);
                DefectRateModel.InvalidatePlot(true);
            }
        }
    }
}