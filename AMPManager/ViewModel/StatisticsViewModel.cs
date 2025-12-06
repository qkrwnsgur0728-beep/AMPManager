using System;
using System.Collections.Generic;
using System.Linq; // ★ Linq 추가 (Axes 검색용)
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
        private ApiService _apiService = new ApiService();

        // 날짜 선택 (초기값: 최근 7일)
        private DateTime _endDate = DateTime.Now;
        private DateTime _startDate = DateTime.Now.AddDays(-6);

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

        // 그래프 모델
        private PlotModel _defectRateModel;
        public PlotModel DefectRateModel
        {
            get => _defectRateModel;
            set => SetProperty(ref _defectRateModel, value);
        }

        // 하단 카드 (불량 개수 표시용)
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

            // 범례 설정
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

                // ★★★ [수정 1] 시작/종료 범위 강제 고정 (주석 해제됨) ★★★
                Minimum = DateTimeAxis.ToDouble(StartDate),
                Maximum = DateTimeAxis.ToDouble(EndDate),

                // 하루(1일) 간격 고정
                IntervalType = DateTimeIntervalType.Days,
                MajorStep = 1.0,

                AxislineColor = gridColor,
                TicklineColor = gridColor,
                TextColor = textColor,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = gridColor
            });

            // Y축
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
            // API 호출
            var stats = await _apiService.GetStatisticsAsync(StartDate, EndDate);

            if (stats == null) return;

            // 1. 하단 카드 갱신
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

                // ★★★ [수정 2] 그래프 X축 범위를 선택한 날짜에 맞춰 업데이트 ★★★
                // (이 코드가 없으면 날짜를 바꿔도 그래프 범위가 그대로입니다)
                var dateAxis = DefectRateModel.Axes.FirstOrDefault(x => x.Position == AxisPosition.Bottom) as DateTimeAxis;
                if (dateAxis != null)
                {
                    dateAxis.Minimum = DateTimeAxis.ToDouble(StartDate);
                    dateAxis.Maximum = DateTimeAxis.ToDouble(EndDate);
                }

                // (1) 전체 검사 수량 (파란선)
                var totalSeries = new LineSeries
                {
                    Title = "전체 검사",
                    Color = OxyColor.Parse("#00C1D4"),
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 3,
                    StrokeThickness = 2
                };

                // (2) 불량 수량 (빨간선)
                var defectSeries = new LineSeries
                {
                    Title = "불량 발생",
                    Color = OxyColor.Parse("#FF5252"),
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 3,
                    StrokeThickness = 2
                };

                foreach (var item in stats.daily_data)
                {
                    if (DateTime.TryParse(item.date, out DateTime dt))
                    {
                        double xValue = DateTimeAxis.ToDouble(dt);
                        totalSeries.Points.Add(new DataPoint(xValue, item.total));
                        defectSeries.Points.Add(new DataPoint(xValue, item.defect));
                    }
                }

                DefectRateModel.Series.Add(totalSeries);
                DefectRateModel.Series.Add(defectSeries);
                DefectRateModel.InvalidatePlot(true);
            }
        }
    }
}