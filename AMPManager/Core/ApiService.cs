using System;
using System.Collections.Generic;
using System.Net.Http;
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

        // ★ 서버 주소 (로컬이 아닌 경우 실제 IP로 변경 필요)
        private const string BaseUrl = "http://192.168.0.28:8000";

        public ApiService()
        {
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(5);
        }

        // [1] 로그인
        public async Task<User?> LoginAsync(string id, string pw)
        {
            try
            {
                var payload = new LoginRequest { Id = id, Pw = pw };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{BaseUrl}/api/login", content);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[Login Response]: {json}"); // 디버깅용 로그

                    var result = JsonConvert.DeserializeObject<LoginResponse>(json);

                    if (result != null && result.Code == 200)
                    {
                        return new User(result.UserName, id, result.Role);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Login Error] {ex.Message}");
            }
            return null;
        }

        // [2] 회원가입 (Role 포함 전송)
        public async Task<bool> SignupAsync(string id, string pw, string name, int role = 2)
        {
            try
            {
                // 서버 규격: { id, pw, name, role }
                var payload = new SignupRequest { Id = id, Pw = pw, Name = name, Role = role };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                // 서버 전송
                var response = await _client.PostAsync($"{BaseUrl}/api/signup", content);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<LoginResponse>(json);

                    // 서버가 보낸 JSON의 "code"가 200이면 성공
                    if (result != null && result.Code == 200)
                    {
                        return true;
                    }
                }
                else
                {
                    // 실패 (401 등) 시 서버 메시지 디버깅
                    string errorMsg = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[Signup Failed] {response.StatusCode}: {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Signup Error] {ex.Message}");
            }
            return false;
        }

        // [3] 로그 조회
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
                    Debug.WriteLine($"[GetLogs Raw]: {json}"); // 데이터가 안 뜨면 이 로그를 확인해야 함

                    var list = JsonConvert.DeserializeObject<List<ServerLogItem>>(json);
                    if (list == null) return new List<LogEntry>();

                    return list.Select(s => new LogEntry
                    {
                        Id = s.mid,
                        Timestamp = s.timestamp,
                        PropertyName = s.product_name,
                        Status = (s.result == "NG" ? "불량" : "정상")
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logs Error] {ex.Message}");
            }
            return new List<LogEntry>();
        }

        // [4] 로그 이미지 조회
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

        // [5] 측정 결과 업로드
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

        // [6] 통계 데이터 조회 (가장 중요한 부분)
        public async Task<ServerStats?> GetStatisticsAsync(DateTime start, DateTime end)
        {
            try
            {
                var payload = new { startDate = start.ToString("yyyy-MM-dd"), endDate = end.ToString("yyyy-MM-dd") };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{BaseUrl}/api/statistics", content);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[Statistics Raw]: {json}"); // ★ 여기에 값이 찍히는지 확인 필수

                    // JSON 매핑이 안 되면 객체는 생성되나 내부 값들이 0이나 null이 됨
                    return JsonConvert.DeserializeObject<ServerStats>(json);
                }
                else
                {
                    Debug.WriteLine($"[Statistics Failed]: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Stats Error] {ex.Message}");
            }
            return null;
        }

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

        // --- 내부 Model 클래스 (JsonProperty 추가됨) ---
        // 서버에서 오는 필드명(예: snake_case)과 정확히 매칭되도록 설정

        private class ServerLogItem
        {
            [JsonProperty("mid")]
            public int mid { get; set; }

            [JsonProperty("timestamp")]
            public string timestamp { get; set; } = "";

            [JsonProperty("product_name")]
            public string product_name { get; set; } = "";

            [JsonProperty("result")]
            public string result { get; set; } = "";
        }
    }

    // --- 통계 관련 모델 클래스 ---
    public class ServerStats
    {
        [JsonProperty("daily_data")]
        public List<DailyStatItem> daily_data { get; set; } = new List<DailyStatItem>();

        [JsonProperty("counts")]
        public DefectCountItem counts { get; set; } = new DefectCountItem();
    }

    public class DailyStatItem
    {
        [JsonProperty("date")]
        public string date { get; set; } = "";

        [JsonProperty("total")]
        public int total { get; set; }

        [JsonProperty("defect")]
        public int defect { get; set; }
    }

    public class DefectCountItem
    {
        // ★ 중요: 서버가 보내는 키 값과 정확히 같아야 함.
        // 예를 들어 서버가 "shape_defect"로 보내는데 여기서 "shape"로 받으면 값이 0이 됨.

        [JsonProperty("shape")]
        public int shape { get; set; }

        [JsonProperty("center")]
        public int center { get; set; }

        [JsonProperty("rust")]
        public int rust { get; set; }

        [JsonProperty("total_ng")]
        public int total_ng { get; set; }
    }
}