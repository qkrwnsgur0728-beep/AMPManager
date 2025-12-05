using System;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;

namespace AMPManager.Model
{
    public class LogEntry
    {
        // [1] DB 데이터
        public int MeasureId { get; set; }
        public int Id => MeasureId;

        public string Timestamp { get; set; }
        public string Status { get; set; }
        public string DefectReason { get; set; }
        public string ProductId { get; set; }
        public string PropertyName { get; set; }

        public string MeasuredContour { get; set; } // 측정 좌표 (JSON)
        public string MeasuredCenter { get; set; }  // 중심 좌표 (JSON)
        public string TemplateData { get; set; }    // ★ 정상 기준 좌표 (JSON)

        public double HoleOffset { get; set; }
        public double AreaSize { get; set; }
        public double ModelScore { get; set; }

        // [2] 기준값
        public double LimitFail { get; set; } = 6.0;
        public double LimitWarn { get; set; } = 4.5;
        public double TolShape { get; set; } = 5.0;
        public double TolHole { get; set; } = 5.0;

        // [3] 이미지
        public string Cam1Path { get; set; }
        public string Cam2Path { get; set; }

        // [4] 그래프 바인딩용
        public Brush StatusColor { get; set; }
        public SeriesCollection ShapeSeriesCollection { get; set; }
        public SeriesCollection DeviationSeriesCollection { get; set; }
        public SeriesCollection ConcentricitySeriesCollection { get; set; }
        public string[] DeviationLabels { get; set; }
        public SectionsCollection DeviationSections { get; set; }
    }
}