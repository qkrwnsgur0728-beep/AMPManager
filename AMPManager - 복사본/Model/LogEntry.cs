using System;
using System.Windows.Media;
using LiveCharts;

namespace AMPManager.Model
{
    // LogEntry.cs 파일에 이 코드를 사용하세요.
    public class LogEntry
    {
        // =======================================================
        // Measurements 테이블 필드 및 기존 정보
        // =======================================================
        public string Timestamp { get; set; }
        public string PropertyName { get; set; }
        public int Id { get; set; } // 제품 ID
        public string Status { get; set; } // 판정 결과 (정상/불량)
        public string DefectReason { get; set; } // 비고/사유

        // Measurements 테이블의 이미지 경로 필드
        public string Cam1Path { get; set; } // cam1_path
        public string Cam2Path { get; set; } // cam2_path

        // Measurements 테이블의 측정 수치 필드
        public double HoleOffset { get; set; } // hole_offset (REAL)
        public double AreaSize { get; set; } // area_size (REAL)
        public double ModelScore { get; set; } // model_score (REAL)

        // =======================================================
        // Product 테이블의 공차 기준 필드
        // =======================================================
        public double LimitFail { get; set; } // limit_fail (불합격 기준)
        public double LimitWarn { get; set; } // limit_warn (경고 기준)

        // =======================================================
        // UI 및 그래프 관련 속성
        // =======================================================
        // 💡 Brush 타입 충돌을 피하기 위해 System.Windows.Media.Brush 타입 사용
        public Brush StatusColor { get; set; }
        public SeriesCollection ChartSeriesCollection { get; set; }
        public string[] ChartLabels { get; set; }
        public Func<double, string> YFormatter { get; set; }
    }
}