using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using AMPManager.Model;

namespace AMPManager.Core
{
    public class DatabaseManager
    {
        private const string ConnectionString = "Data Source=factory.db;Version=3;";

        public DatabaseManager()
        {
            // 테이블 확인 로직 (생략 가능)
        }

        // ★ [핵심] 상세 데이터 조회 (TemplateData 포함)
        public LogEntry GetLogDetail(int measureId)
        {
            if (!File.Exists("factory.db")) return null;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            M.*, 
                            P.product_name, P.limit_fail, P.limit_warn, 
                            P.tol_hole, P.tol_shape, P.template_data
                        FROM Measurements M
                        LEFT JOIN Product P ON M.product_id = P.product_id
                        WHERE M.measure_id = @id";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", measureId);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new LogEntry
                                {
                                    MeasureId = Convert.ToInt32(reader["measure_id"]),
                                    Timestamp = reader["measured_at"].ToString(),
                                    Status = reader["inspection_result"].ToString(),
                                    DefectReason = reader["fail_reason"]?.ToString(),
                                    PropertyName = reader["product_name"]?.ToString(),

                                    MeasuredContour = reader["measured_contour"]?.ToString(),
                                    MeasuredCenter = reader["measured_center"]?.ToString(),
                                    TemplateData = reader["template_data"]?.ToString(), // ★ DB 좌표 로드

                                    Cam1Path = reader["cam1_path"]?.ToString(),
                                    Cam2Path = reader["cam2_path"]?.ToString(),
                                    HoleOffset = reader["hole_offset"] != DBNull.Value ? Convert.ToDouble(reader["hole_offset"]) : 0.0,
                                    AreaSize = reader["area_size"] != DBNull.Value ? Convert.ToDouble(reader["area_size"]) : 0.0,

                                    LimitFail = reader["limit_fail"] != DBNull.Value ? Convert.ToDouble(reader["limit_fail"]) : 6.0,
                                    LimitWarn = reader["limit_warn"] != DBNull.Value ? Convert.ToDouble(reader["limit_warn"]) : 4.5,
                                    TolHole = reader["tol_hole"] != DBNull.Value ? Convert.ToDouble(reader["tol_hole"]) : 5.0,
                                    TolShape = reader["tol_shape"] != DBNull.Value ? Convert.ToDouble(reader["tol_shape"]) : 5.0
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"상세 조회 실패: {ex.Message}");
            }
            return null;
        }

        // ... (나머지 로그인, 목록 조회 등 기존 메서드 유지) ...
        // 필요하다면 기존 코드의 Login, GetLogsDirect 등을 그대로 두세요.
    }
}