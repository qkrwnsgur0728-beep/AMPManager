using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers; // [추가] 헤더 처리를 위해 필요
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AMPManager.Model;
using System.Diagnostics;
using System.Linq;

namespace AMPManager.Core
{
    public class ApiService
    {
        private readonly HttpClient _client;

        // ★ [중요] 실제 파이썬 서버 IP로 변경하세요 (로컬 테스트 시 localhost 유지)
        private const string BaseUrl = "http://localhost:8000";

        public ApiService()
        {
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(5);
        }

        // [1] 로그인 (수정됨: bool -> User?)
        public async Task<User?> LoginAsync(string id, string pw)
        {
            try
            {
                // 비밀번호는 서버가 SHA256->Bcrypt 하므로 평문 전송
                var payload = new { username = id, password = pw };

                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                // 서버로 POST 요청 전송
                var response = await _client.PostAsync($"{BaseUrl}/api/login", content);

                if (response.IsSuccessStatusCode)
                {
                    // 성공 시 응답(JSON) 파싱
                    string json = await response.Content.ReadAsStringAsync();
                    var loginRes = JsonConvert.DeserializeObject<LoginResponse>(json);

                    if (loginRes != null && !string.IsNullOrEmpty(loginRes.AccessToken))
                    {
                        // 토큰 저장 (이후 요청 헤더에 추가)
                        _client.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", loginRes.AccessToken);

                        // 반환된 정보로 User 객체 생성 (정보가 없으면 기본값 사용)
                        string name = loginRes.UserName ?? id; // 이름 없으면 ID 사용
                        int role = loginRes.Role ?? 2;         // 권한 없으면 일반(2)로 처리

                        return new User(name, id, role);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Login Error] {ex.Message}");
            }

            return null; // 실패 시 null 반환
        }

        // --- [기존 기능 유지] ---

        // [2] 로그 리스트 가져오기 (DB 조회)
        public async Task<List<LogEntry>> GetLogsAsync(string date)
        {
            try
            {
                var payload = new { startDate = date };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{BaseUrl}/api/logs", content);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var list = JsonConvert.DeserializeObject<List<ServerLogItem>>(json);

                    if (list == null) return new List<LogEntry>();

                    // 서버 데이터(timestamp, result)를 WPF 화면용(LogEntry)으로 변환
                    return list.Select(s => new LogEntry
                    {
                        Id = s.mid,
                        Timestamp = s.timestamp,
                        PropertyName = s.product_name,
                        Status = (s.result == "NG" ? "불량" : "정상")
                    }).ToList();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Logs Error] {ex.Message}"); }
            return new List<LogEntry>();
        }

        // [3] 사진 데이터 가져오기 (상세 보기용)
        public async Task<(byte[]?, byte[]?)> GetLogImagesAsync(int mid)
        {
            try
            {
                var response = await _client.GetAsync($"{BaseUrl}/api/logs/{mid}/images");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic data = JsonConvert.DeserializeObject(json);

                    string s1 = data.img1_base64;
                    string s2 = data.img2_base64;

                    byte[]? b1 = !string.IsNullOrEmpty(s1) ? Convert.FromBase64String(s1) : null;
                    byte[]? b2 = !string.IsNullOrEmpty(s2) ? Convert.FromBase64String(s2) : null;

                    return (b1, b2);
                }
            }
            catch { }
            return (null, null);
        }

        // [4] 측정 데이터 업로드
        public async Task UploadMeasurementAsync(int pid, string result, byte[]? img1, byte[]? img2)
        {
            try
            {
                var payload = new
                {
                    pid = pid,
                    result = result,
                    img1_base64 = img1 != null ? Convert.ToBase64String(img1) : null,
                    img2_base64 = img2 != null ? Convert.ToBase64String(img2) : null
                };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                await _client.PostAsync($"{BaseUrl}/api/measurements", content);
            }
            catch { }
        }

        // [5] 통계 데이터 조회
        public async Task<ServerStats?> GetStatisticsAsync(DateTime start, DateTime end)
        {
            try
            {
                var payload = new { startDate = start.ToString("yyyy-MM-dd"), endDate = end.ToString("yyyy-MM-dd") };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _client.PostAsync($"{BaseUrl}/api/statistics", content);
                if (response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<ServerStats>(await response.Content.ReadAsStringAsync());
                }
            }
            catch { }
            return null;
        }

        // [6] 시스템 제어
        public async Task<ServerData?> GetStatusAsync() { return null; }
        public async Task<bool> StartSystemAsync(string id = "1") => await PostCmd("/api/start", id);
        public async Task<bool> RestartSystemAsync(string id = "1") => await PostCmd("/api/restart", id);
        public async Task<bool> StopSystemAsync(string id = "1") => await PostCmd("/api/stop", id);
        public async Task<bool> ControlCctvAsync(string action) => true;

        private async Task<bool> PostCmd(string url, string id)
        {
            try { return (await _client.PostAsync(BaseUrl + url, new StringContent(JsonConvert.SerializeObject(new { deviceId = id }), Encoding.UTF8, "application/json"))).IsSuccessStatusCode; }
            catch { return false; }
        }

        private class ServerLogItem
        {
            public int mid { get; set; }
            public string timestamp { get; set; }
            public string product_name { get; set; }
            public string result { get; set; }
        }
    }

    public class ServerStats
    {
        public List<DailyStatItem> daily_data { get; set; }
        public DefectCountItem counts { get; set; }
    }

    public class DailyStatItem
    {
        public string date { get; set; }
        public int total { get; set; }
        public int defect { get; set; }
    }

    public class DefectCountItem
    {
        public int shape { get; set; }
        public int center { get; set; }
        public int rust { get; set; }
        public int total_ng { get; set; }
    }
}