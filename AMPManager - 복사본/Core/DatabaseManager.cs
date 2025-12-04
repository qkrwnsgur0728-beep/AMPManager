using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using AMPManager.Model;

namespace AMPManager.Core
{
    public class DatabaseManager
    {
        // DB 파일 경로 (실행 파일과 같은 폴더)
        private const string ConnectionString = "Data Source=factory.db;Version=3;";

        public DatabaseManager()
        {
            EnsureTableStructure();
        }

        // 1. 로그인 (대문자 User 테이블, 평문 비교)
        public User? Login(string id, string pw)
        {
            if (!File.Exists("factory.db")) return null;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // ★수정: 대문자 User 테이블 사용
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 로그인 실패: {ex.Message}");
            }
            return null;
        }

        // ★★★ 2. 로그 조회 (이 함수가 없어서 에러가 났습니다!) ★★★
        public List<LogEntry> GetLogsDirect(string targetDate)
        {
            var list = new List<LogEntry>();
            if (!File.Exists("factory.db")) return list;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();

                    // 대문자 Measurements, Product 테이블 사용
                    // 날짜 검색: LIKE '2025-11-29%'
                    string query = @"
                        SELECT 
                            M.measure_id, 
                            M.measured_at, 
                            P.product_name, 
                            M.inspection_result
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
                                    Id = Convert.ToInt32(reader["measure_id"]),
                                    Timestamp = reader["measured_at"].ToString(),
                                    PropertyName = pName,
                                    Status = res == "NG" ? "불량" : "정상"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로그 조회 실패: {ex.Message}");
            }
            return list;
        }

        // 3. 회원가입 (대문자 User 테이블)
        public bool RegisterUser(string id, string pw, string name, int role)
        {
            if (!File.Exists("factory.db")) return false;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();

                    // 아이디 중복 체크
                    string checkQuery = "SELECT COUNT(*) FROM User WHERE login_id = @id";
                    using (SQLiteCommand checkCmd = new SQLiteCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@id", id);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());
                        if (count > 0) return false;
                    }

                    // 사용자 등록
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"회원가입 실패: {ex.Message}");
                return false;
            }
        }

        // 4. 이미지 가져오기
        public (byte[]?, byte[]?) GetLogImages(int mid)
        {
            if (!File.Exists("factory.db")) return (null, null);
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // 이미지 컬럼(cam1_path 등)이 텍스트인지 BLOB인지에 따라 다르지만,
                    // 현재는 에러 방지를 위해 null을 리턴하거나 경로를 읽도록 둠
                    string query = "SELECT cam1_path, cam2_path FROM Measurements WHERE measure_id = @mid";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@mid", mid);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // 실제 구현 시에는 파일 경로를 읽어 byte[]로 변환하거나 BLOB을 읽어야 함
                                return (null, null);
                            }
                        }
                    }
                }
            }
            catch { }
            return (null, null);
        }

        // 5. 데이터 삽입 (HomeViewModel용)
        public void InsertMeasurement(int productId, string time, bool isDefect, byte[]? img1, byte[]? img2)
        {
            if (!File.Exists("factory.db")) return;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string resultStr = isDefect ? "NG" : "OK";

                    string query = "INSERT INTO Measurements (product_id, measured_at, inspection_result, cam1_path, cam2_path) VALUES (@pid, @time, @res, '', '')";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@pid", productId);
                        cmd.Parameters.AddWithValue("@time", time);
                        cmd.Parameters.AddWithValue("@res", resultStr);
                        // 이미지는 현재 DB 스키마상 TEXT 경로로 되어 있으므로 빈 문자열 처리 (추후 구현 필요)
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 저장 실패: {ex.Message}");
            }
        }

        // 6. 통계 메서드 (빈 구현 - 에러 방지)
        public Dictionary<string, double> GetDailyDefectRates(DateTime start, DateTime end)
        {
            return new Dictionary<string, double>();
        }

        public (double w, double l, double c, double cp) GetAverageSpecs()
        {
            return (0, 0, 0, 0);
        }

        // 7. 테이블 구조 확인 (기존 유지)
        private void EnsureTableStructure()
        {
            // 이미 DB가 존재하므로 로직 최소화
        }

        // 8. 기존 호환성 유지용 (GetLogs -> GetLogsDirect 호출)
        public List<LogEntry> GetLogs(string targetDate)
        {
            return GetLogsDirect(targetDate);
        }

        public void ImportImagesFromFolder() { }
    }
}