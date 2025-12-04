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

        // 1. 로그인 (User 테이블 사용)
        public User? Login(string id, string pw)
        {
            if (!File.Exists("factory.db")) return null;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // 실제 DB 컬럼명: login_id, password_hash, user_name, role
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

        // 2. 로그 조회 (DB 직접 조회 - API 대체)
        public List<LogEntry> GetLogsDirect(string targetDate)
        {
            var list = new List<LogEntry>();
            if (!File.Exists("factory.db")) return list;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();

                    // Measurements 테이블과 Product 테이블 조인
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

        // (호환성 유지용) 기존 GetLogs 메서드
        public List<LogEntry> GetLogs(string targetDate)
        {
            return GetLogsDirect(targetDate);
        }

        // 3. 이미지 가져오기
        public (byte[]?, byte[]?) GetLogImages(int mid)
        {
            if (!File.Exists("factory.db")) return (null, null);
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // 이미지 컬럼이 있는지 확인하고 가져옴 (없으면 null 반환)
                    string query = "SELECT img_cam1, img_cam2 FROM Measurements WHERE measure_id = @mid";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@mid", mid);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                byte[]? i1 = reader["img_cam1"] as byte[];
                                byte[]? i2 = reader["img_cam2"] as byte[];
                                return (i1, i2);
                            }
                        }
                    }
                }
            }
            catch { }
            return (null, null);
        }

        // 4. 데이터 삽입 (HomeViewModel에서 사용) - 누락되었던 부분 복구
        public void InsertMeasurement(int productId, string time, bool isDefect, byte[]? img1, byte[]? img2)
        {
            if (!File.Exists("factory.db")) return;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string resultStr = isDefect ? "NG" : "OK";

                    // 컬럼명을 DB 스키마에 맞게 수정: productID -> product_id, measurement_time -> measured_at, result -> inspection_result
                    string query = "INSERT INTO Measurements (product_id, measured_at, inspection_result, img_cam1, img_cam2) VALUES (@pid, @time, @res, @img1, @img2)";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@pid", productId);
                        cmd.Parameters.AddWithValue("@time", time);
                        cmd.Parameters.AddWithValue("@res", resultStr);
                        cmd.Parameters.AddWithValue("@img1", img1 ?? new byte[0]);
                        cmd.Parameters.AddWithValue("@img2", img2 ?? new byte[0]);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 저장 실패: {ex.Message}");
            }
        }

        // 5. 통계 메서드 (StatisticsViewModel에서 사용) - 누락되었던 부분 복구
        public Dictionary<string, double> GetDailyDefectRates(DateTime start, DateTime end)
        {
            // 임시로 빈 딕셔너리 반환 (오류 방지)
            return new Dictionary<string, double>();
        }

        public (double w, double l, double c, double cp) GetAverageSpecs()
        {
            // 임시로 0 반환 (오류 방지)
            return (0, 0, 0, 0);
        }

        // 6. 테이블 구조 확인 및 생성
        private void EnsureTableStructure()
        {
            if (!File.Exists("factory.db")) return;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // 필요한 이미지 컬럼이 없으면 추가
                    var cols = new[] { "img_cam1", "img_cam2" };
                    foreach (var col in cols)
                    {
                        try
                        {
                            // 테이블 이름 Measurements로 수정
                            using (SQLiteCommand cmd = new SQLiteCommand($"ALTER TABLE Measurements ADD COLUMN {col} BLOB", conn))
                                cmd.ExecuteNonQuery();
                        }
                        catch { } // 이미 있으면 무시
                    }
                }
            }
            catch { }
        }

        // 7. 폴더 이미지 임포트 (기존 기능)
        public void ImportImagesFromFolder()
        {
            // 기능이 필요하다면 구현, 현재는 에러 방지를 위해 비워둠
        }
    }
}