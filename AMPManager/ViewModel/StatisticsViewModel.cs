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
            // 초기화 로직 (필요시 추가)
        }

        // =============================================================
        // [1] 로그인 기능
        // =============================================================
        public User? Login(string id, string pw)
        {
            if (!File.Exists("factory.db")) return null;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string query = "SELECT user_name, role FROM User WHERE login_id = @id AND password_hash = @pw";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@pw", pw);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new User(
                                    reader["user_name"].ToString(),
                                    id,
                                    Convert.ToInt32(reader["role"])
                                );
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // =============================================================
        // [2] 로그 목록 조회
        // =============================================================
        public List<LogEntry> GetLogsDirect(string targetDate)
        {
            var list = new List<LogEntry>();
            if (!File.Exists("factory.db")) return list;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT M.measure_id, M.measured_at, P.product_name, M.inspection_result
                        FROM Measurements M
                        LEFT JOIN Product P ON M.product_id = P.product_id
                        WHERE M.measured_at LIKE @date || '%'
                        ORDER BY M.measured_at DESC";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@date", targetDate);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string res = reader["inspection_result"].ToString();
                                if (string.IsNullOrEmpty(res)) res = "OK";
                                string pName = reader["product_name"] is DBNull ? "Unknown" : reader["product_name"].ToString();

                                list.Add(new LogEntry
                                {
                                    MeasureId = Convert.ToInt32(reader["measure_id"]),
                                    Timestamp = reader["measured_at"].ToString(),
                                    PropertyName = pName,
                                    Status = res == "NG" ? "불량" : "정상"
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        // =============================================================
        // [3] 상세 데이터 조회 (그래프용)
        // =============================================================
        public LogEntry GetLogDetail(int measureId)
        {
            if (!File.Exists("factory.db")) return null;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT M.*, P.product_name, P.limit_fail, P.limit_warn, P.tol_hole, P.tol_shape, P.template_data
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
                                    TemplateData = reader["template_data"]?.ToString(),
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
            catch { }
            return null;
        }

        // =============================================================
        // [4] 통계 기능 (★ 누락되었던 부분 복구 ★)
        // =============================================================

        // 일별 불량률 조회
        public Dictionary<string, double> GetDailyDefectRates(DateTime start, DateTime end)
        {
            var rates = new Dictionary<string, double>();
            if (!File.Exists("factory.db")) return rates;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // 날짜별 (전체 개수, 불량 개수) 조회
                    string query = @"
                        SELECT substr(measured_at, 1, 10) as date, 
                               COUNT(*) as total, 
                               SUM(CASE WHEN inspection_result = 'NG' THEN 1 ELSE 0 END) as ng_count
                        FROM Measurements
                        WHERE date(measured_at) BETWEEN date(@start) AND date(@end)
                        GROUP BY date";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd"));

                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string date = reader["date"].ToString();
                                double total = Convert.ToDouble(reader["total"]);
                                double ng = Convert.ToDouble(reader["ng_count"]);
                                rates[date] = (total > 0) ? (ng / total * 100.0) : 0.0;
                            }
                        }
                    }
                }
            }
            catch { }
            return rates;
        }

        // 평균 스펙 조회
        public (double w, double l, double c, double cp) GetAverageSpecs()
        {
            if (!File.Exists("factory.db")) return (0, 0, 0, 0);
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // 최근 100건 데이터의 평균 계산
                    string query = "SELECT AVG(area_size), AVG(model_score), AVG(hole_offset) FROM Measurements ORDER BY measure_id DESC LIMIT 100";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                double area = reader[0] != DBNull.Value ? Convert.ToDouble(reader[0]) : 0;
                                double score = reader[1] != DBNull.Value ? Convert.ToDouble(reader[1]) : 0;
                                double offset = reader[2] != DBNull.Value ? Convert.ToDouble(reader[2]) : 0;
                                return (area, score, offset, 0); // (Width, Length, Contour, Center 대신 Area, Score, Offset 매핑)
                            }
                        }
                    }
                }
            }
            catch { }
            return (0, 0, 0, 0);
        }

        // =============================================================
        // [5] 기타 (회원가입, 데이터 삽입 등)
        // =============================================================
        public bool RegisterUser(string id, string pw, string name, int role)
        {
            if (!File.Exists("factory.db")) return false;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string checkQuery = "SELECT COUNT(*) FROM User WHERE login_id = @id";
                    using (SQLiteCommand checkCmd = new SQLiteCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@id", id);
                        if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0) return false;
                    }
                    string query = "INSERT INTO User (user_name, login_id, password_hash, role) VALUES (@name, @id, @pw, @role)";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@pw", pw);
                        cmd.Parameters.AddWithValue("@role", role);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch { return false; }
        }

        public void InsertMeasurement(int productId, string time, bool isDefect, byte[]? img1, byte[]? img2)
        {
            // 테스트용 데이터 삽입 로직 (필요시 구현)
        }

        // 호환성 유지용 메서드
        public List<LogEntry> GetLogs(string targetDate) => GetLogsDirect(targetDate);
        public void ImportImagesFromFolder() { }
    }
}