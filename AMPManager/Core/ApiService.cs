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

        // ★ 서버 주소 (환경에 맞게 확인)
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
                var payload = new { id = id, pw = pw };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{BaseUrl}/api/login", content);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<LoginResponse>(json);
                    if (result != null && result.Code == 200)
                    {
                        return new User(result.UserName, id, result.Role);
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Login Error] {ex.Message}"); }
            return null;
        }

        // [2] 회원가입
        public async Task<bool> SignupAsync(string id, string pw, string name, int role = 2)
        {
            try
            {
                var payload = new { id = id, pw = pw, name = name, role = role };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _client.PostAsync($"{BaseUrl}/api/signup", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<LoginResponse>(await response.Content.ReadAsStringAsync());
                    return result != null && result.Code == 200;
                }
            }
            catch { }
            return false;
        }

        // [3] 로그 목록 조회 (start_date 사용 안함 -> startDate)
        public async Task<List<LogEntry>> GetLogsAsync(string date)
        {
            try
            {
                // [확인됨] 서버가 'startDate' 파라미터를 요구함
                var payload = new { startDate = date };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _client.PostAsync($"{BaseUrl}/api/logs", content);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var list = JsonConvert.DeserializeObject<List<ServerLogItem>>(json);
                    if (list == null) return new List<LogEntry>();

                    return list.Select(s => new LogEntry
                    {
                        Id = s.mid,
                        Timestamp = s.timestamp,
                        PropertyName = s.product_name,
                        Status = (s.result == "NG" ? "불량" : "정상"),
                        DefectReason = (s.result == "NG" ? "치수 오차 초과" : "-")
                    }).ToList();
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Logs Error] {ex.Message}"); }
            return new List<LogEntry>();
        }

        // [4] ★ 로그 상세 정보 조회 (그래프 데이터 파싱 문제 해결)
        public async Task<LogEntry?> GetLogDetailAsync(int mid)
        {
            try
            {
                var response = await _client.GetAsync($"{BaseUrl}/api/logs/{mid}");
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[Detail Raw]: {json}"); // 데이터 들어오는지 확인

                    // JSON 파싱 시도
                    var item = JsonConvert.DeserializeObject<ServerLogDetail>(json);
                    if (item == null) return null;

                    return new LogEntry
                    {
                        Id = item.mid,
                        Timestamp = item.timestamp,
                        PropertyName = item.product_name,
                        Status = (item.result == "NG" ? "불량" : "정상"),

                        // ★ 중요: object로 받은 데이터를 문자열로 변환하여 할당
                        MeasuredContour = item.measured_contour?.ToString(),
                        MeasuredCenter = item.measured_center?.ToString(),
                        TemplateData = item.template_data?.ToString(),

                        TolShape = item.tol_shape,
                        TolHole = item.tol_hole,
                        LimitWarn = item.limit_warn,
                        LimitFail = item.limit_fail,

                        HoleOffset = item.hole_offset,
                        AreaSize = item.area_size,
                        ModelScore = item.model_score,
                        DefectReason = (item.result == "NG" ? "치수 오차 초과" : "-")
                    };
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[LogDetail Error] {ex.Message}"); }
            return null;
        }

        // [5] 로그 이미지 조회
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

        // [6] 통계 조회
        public async Task<ServerStats?> GetStatisticsAsync(DateTime start, DateTime end)
        {
            try
            {
                var payload = new { startDate = start.ToString("yyyy-MM-dd"), endDate = end.ToString("yyyy-MM-dd") };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _client.PostAsync($"{BaseUrl}/api/statistics", content);
                if (response.IsSuccessStatusCode)
                    return JsonConvert.DeserializeObject<ServerStats>(await response.Content.ReadAsStringAsync());
            }
            catch { }
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

        // --- 내부 DTO 클래스들 ---
        private class ServerLogItem
        {
            [JsonProperty("mid")] public int mid { get; set; }
            [JsonProperty("timestamp")] public string timestamp { get; set; } = "";
            [JsonProperty("product_name")] public string product_name { get; set; } = "";
            [JsonProperty("result")] public string result { get; set; } = "";
        }

        // ★ 상세 정보용 DTO (타입 수정됨: string -> object)
        private class ServerLogDetail : ServerLogItem
        {
            // 서버가 JSON 객체로 보내도 받을 수 있게 object로 선언
            [JsonProperty("measured_contour")] public object measured_contour { get; set; }
            [JsonProperty("measured_center")] public object measured_center { get; set; }
            [JsonProperty("template_data")] public object template_data { get; set; }

            [JsonProperty("tol_shape")] public double tol_shape { get; set; }
            [JsonProperty("tol_hole")] public double tol_hole { get; set; }
            [JsonProperty("limit_warn")] public double limit_warn { get; set; }
            [JsonProperty("limit_fail")] public double limit_fail { get; set; }

            [JsonProperty("hole_offset")] public double hole_offset { get; set; }
            [JsonProperty("area_size")] public double area_size { get; set; }
            [JsonProperty("model_score")] public double model_score { get; set; }
        }
    }

    public class ServerStats
    {
        [JsonProperty("daily_data")] public List<DailyStatItem> daily_data { get; set; } = new List<DailyStatItem>();
        [JsonProperty("counts")] public DefectCountItem counts { get; set; } = new DefectCountItem();
    }
    public class DailyStatItem
    {
        [JsonProperty("date")] public string date { get; set; } = "";
        [JsonProperty("total")] public int total { get; set; }
        [JsonProperty("defect")] public int defect { get; set; }
    }
    public class DefectCountItem
    {
        [JsonProperty("shape")] public int shape { get; set; }
        [JsonProperty("center")] public int center { get; set; }
        [JsonProperty("rust")] public int rust { get; set; }
        [JsonProperty("total_ng")] public int total_ng { get; set; }
    }
    public class LoginResponse
    {
        [JsonProperty("code")] public int Code { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("user_name")] public string UserName { get; set; }
        [JsonProperty("role")] public int Role { get; set; }
    }
}