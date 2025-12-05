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

        public DatabaseManager() { }

        // 1. 로그인
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            return null;
        }

        // 2. 로그 목록 조회
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            return list;
        }

        // 3. 상세 조회
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

        // 4. 이미지 가져오기
        public (byte[]?, byte[]?) GetLogImages(int mid)
        {
            byte[]? img1 = null;
            byte[]? img2 = null;
            if (!File.Exists("factory.db")) return (null, null);
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string query = "SELECT cam1_path, cam2_path FROM Measurements WHERE measure_id = @mid";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@mid", mid);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string p1 = reader["cam1_path"]?.ToString();
                                string p2 = reader["cam2_path"]?.ToString();
                                if (!string.IsNullOrEmpty(p1) && File.Exists(p1)) img1 = File.ReadAllBytes(p1);
                                if (!string.IsNullOrEmpty(p2) && File.Exists(p2)) img2 = File.ReadAllBytes(p2);
                            }
                        }
                    }
                }
            }
            catch { }
            return (img1, img2);
        }

        // 5. 회원가입 등 기타
        public bool RegisterUser(string id, string pw, string name, int role)
        {
            if (!File.Exists("factory.db")) return false;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string check = "SELECT COUNT(*) FROM User WHERE login_id = @id";
                    using (SQLiteCommand c = new SQLiteCommand(check, conn))
                    {
                        c.Parameters.AddWithValue("@id", id);
                        if (Convert.ToInt32(c.ExecuteScalar()) > 0) return false;
                    }
                    string ins = "INSERT INTO User (user_name, login_id, password_hash, role) VALUES (@name, @id, @pw, @role)";
                    using (SQLiteCommand c = new SQLiteCommand(ins, conn))
                    {
                        c.Parameters.AddWithValue("@name", name);
                        c.Parameters.AddWithValue("@id", id);
                        c.Parameters.AddWithValue("@pw", pw);
                        c.Parameters.AddWithValue("@role", role);
                        c.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch { return false; }
        }

        public void InsertMeasurement(int pid, string time, bool isDefect, byte[]? i1, byte[]? i2)
        {
            if (!File.Exists("factory.db")) return;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string q = "INSERT INTO Measurements (product_id, measured_at, inspection_result, cam1_path, cam2_path) VALUES (@p, @t, @r, '', '')";
                    using (SQLiteCommand c = new SQLiteCommand(q, conn))
                    {
                        c.Parameters.AddWithValue("@p", pid);
                        c.Parameters.AddWithValue("@t", time);
                        c.Parameters.AddWithValue("@r", isDefect ? "NG" : "OK");
                        c.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        public Dictionary<string, double> GetDailyDefectRates(DateTime start, DateTime end)
        {
            var rates = new Dictionary<string, double>();
            if (!File.Exists("factory.db")) return rates;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
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

        public (double w, double l, double c, double cp) GetAverageSpecs() { return (0, 0, 0, 0); }
        public List<LogEntry> GetLogs(string d) => GetLogsDirect(d);
        public void ImportImagesFromFolder() { }
    }
}