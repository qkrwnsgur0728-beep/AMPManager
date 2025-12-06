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
        private ApiService _apiService = new ApiService();

        // 1. 기간 선택 (기본값: 최근 7일)
        private DateTime _startDate = DateTime.Now.AddDays(-6);
        private DateTime _endDate = DateTime.Now;

        public DateTime StartDate
        {
            get => _startDate;
            set { SetProperty(ref _startDate, value); } // 날짜 변경 시 자동 로드 제거 (조회 버튼으로만 동작)
        }

        public DateTime EndDate
        {
            get => _endDate;
            set { SetProperty(ref _endDate, value); } // 날짜 변경 시 자동 로드 제거
        }

        public ICommand SearchCommand { get; }

        // 2. 그래프 모델
        private PlotModel _defectRateModel;
        public PlotModel DefectRateModel
        {
            get => _defectRateModel;
            set => SetProperty(ref _defectRateModel, value);
        }

        // 3. 통계 수치 (개수 표시)
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
            LoadChartData(); // 초기 로드
        }

        private void InitializeChart()
        {
            var model = new PlotModel { Title = "" };
            var textColor = OxyColor.Parse("#E0E0E0");
            var gridColor = OxyColor.Parse("#4A4A5A");

            model.Background = OxyColors.Transparent;
            model.PlotAreaBorderColor = OxyColors.Transparent;
            model.TextColor = textColor;

            // 범례 추가
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
                MajorStep = 1.0,
                // 초기 범위 설정
                Minimum = DateTimeAxis.ToDouble(StartDate),
                Maximum = DateTimeAxis.ToDouble(EndDate.AddDays(1)) // 하루 뒤까지 여유 있게
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
            // 1. API 호출
            var stats = await _apiService.GetStatisticsAsync(StartDate, EndDate);

            if (stats == null) return;

            // 2. 하단 카드 (개수) 갱신
            if (stats.counts != null)
            {
                CntShape = $"{stats.counts.shape} 개";
                CntCenter = $"{stats.counts.center} 개";
                CntRust = $"{stats.counts.rust} 개";
                CntTotal = $"{stats.counts.total_ng} 개";
            }

            // 3. 그래프 갱신
            if (DefectRateModel != null)
            {
                DefectRateModel.Series.Clear();

                // ★★★ [수정] X축 범위를 강제로 업데이트 ★★★
                var dateAxis = DefectRateModel.Axes.FirstOrDefault(x => x.Position == AxisPosition.Bottom) as DateTimeAxis;
                if (dateAxis != null)
                {
                    // 시작일 00:00
                    dateAxis.Minimum = DateTimeAxis.ToDouble(StartDate);
                    // 종료일 다음날 00:00 (그래야 종료일 데이터가 그래프 끝에 안 걸리고 잘 보임)
                    dateAxis.Maximum = DateTimeAxis.ToDouble(EndDate.AddDays(1));
                }

                // 라인 1: 전체 검사 (파란색)
                var totalSeries = new LineSeries
                {
                    Title = "전체 검사",
                    Color = OxyColor.Parse("#00C1D4"),
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4,
                    StrokeThickness = 3
                };

                // 라인 2: 불량 수 (빨간색)
                var defectSeries = new LineSeries
                {
                    Title = "불량 수",
                    Color = OxyColor.Parse("#FF5252"),
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4,
                    StrokeThickness = 3
                };

                if (stats.daily_data != null)
                {
                    foreach (var item in stats.daily_data)
                    {
                        if (DateTime.TryParse(item.date, out DateTime dt))
                        {
                            double xVal = DateTimeAxis.ToDouble(dt);
                            totalSeries.Points.Add(new DataPoint(xVal, item.total));
                            defectSeries.Points.Add(new DataPoint(xVal, item.defect));
                        }
                    }
                }

                DefectRateModel.Series.Add(totalSeries);
                DefectRateModel.Series.Add(defectSeries);

                // ★★★ [중요] 축 범위를 포함한 모든 상태를 리셋하고 다시 그림 ★★★
                DefectRateModel.ResetAllAxes();
                DefectRateModel.InvalidatePlot(true);
            }
        }
    }
}