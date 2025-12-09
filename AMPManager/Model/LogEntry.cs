using System;
using System.Collections.Generic;
// [중요] 모호함 방지를 위해 전체 경로 사용
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;

namespace AMPManager.Model
{
    public class LogEntry
    {
        // ============================
        // 1. 기본 데이터
        // ============================
        public int MeasureId { get; set; }
        public int Id { get => MeasureId; set => MeasureId = value; } // 호환성 유지

        public string Timestamp { get; set; }
        public string Status { get; set; }
        public string DefectReason { get; set; }
        public string ProductId { get; set; }
        public string PropertyName { get; set; }

        // ============================
        // 2. 측정 수치 및 JSON 데이터
        // ============================
        public double HoleOffset { get; set; }
        public double AreaSize { get; set; }
        public double ModelScore { get; set; }

        public string MeasuredContour { get; set; }
        public string MeasuredCenter { get; set; }
        public string TemplateData { get; set; }

        // ============================
        // 3. 기준값 (공차)
        // ============================
        public double LimitFail { get; set; } = 6.0;
        public double LimitWarn { get; set; } = 4.5;
        public double TolShape { get; set; } = 5.0;
        public double TolHole { get; set; } = 5.0;

        // ============================
        // 4. 이미지 (경로 및 소스)
        // ============================
        public string Cam1Path { get; set; }
        public string Cam2Path { get; set; }

        // [화면 표시용] 컴파일 오류 해결을 위해 필수
        public ImageSource Img1 { get; set; }
        public ImageSource Img2 { get; set; }

        // ============================
        // 5. 그래프 및 UI 바인딩 (모두 포함)
        // ============================
        public System.Windows.Media.Brush StatusColor { get; set; } // System.Windows.Media.Brush

        // (버전 1: 단순 차트용)
        public SeriesCollection ChartSeriesCollection { get; set; }
        public string[] ChartLabels { get; set; }

        // (버전 2: 상세 분석용)
        public SeriesCollection ShapeSeriesCollection { get; set; }
        public SeriesCollection DeviationSeriesCollection { get; set; }
        public SeriesCollection ConcentricitySeriesCollection { get; set; }
        public string[] DeviationLabels { get; set; }
        public SectionsCollection DeviationSections { get; set; }

        // 공통
        public Func<double, string> YFormatter { get; set; }

        public VisualElementsCollection ShapeVisuals { get; set; }

        // [변경 시작] 생성자 추가: 리스트들이 Null이 되지 않도록 초기화
        public LogEntry()
        {
            ChartSeriesCollection = new SeriesCollection();
            ShapeSeriesCollection = new SeriesCollection();
            DeviationSeriesCollection = new SeriesCollection();
            ConcentricitySeriesCollection = new SeriesCollection();

            ChartLabels = new string[] { };
            DeviationLabels = new string[] { };

            ShapeVisuals = new VisualElementsCollection();
            DeviationSections = new SectionsCollection();
        }
        // [변경 끝]
    }
}