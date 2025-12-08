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

        // [2] [수정됨] 회원가입 (Role 포함 전송)
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

        // --- [기존 기능 유지] ---

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
            public string timestamp { get; set; } = "";
            public string product_name { get; set; } = "";
            public string result { get; set; } = "";
        }
    }

    public class ServerStats
    {
        public List<DailyStatItem> daily_data { get; set; } = new List<DailyStatItem>();
        public DefectCountItem counts { get; set; } = new DefectCountItem();
    }

    public class DailyStatItem
    {
        public string date { get; set; } = "";
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