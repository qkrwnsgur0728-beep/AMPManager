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
    // 로그인 응답 모델
    public class LoginResponse
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }
    }

    public class ApiService
    {
        private readonly HttpClient _client;

        // ★ 서버 주소 (환경에 맞게 수정하세요)
        //private const string BaseUrl = "http://192.168.0.6:8000";
        private const string BaseUrl = "http://localhost:8000";

        public ApiService()
        {
            _client = new HttpClient();

            // [변경 전] 5초는 너무 짧아서 타임아웃 발생
            // _client.Timeout = TimeSpan.FromSeconds(5);

            // [변경 후] 타임아웃을 30초로 늘림 (네트워크 지연 대비)
            _client.Timeout = TimeSpan.FromSeconds(30);
        }

        // =========================================================
        // [1] 로그인
        // =========================================================
        public async Task<LoginResponse> LoginAsync(string id, string pw)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(pw))
            {
                return new LoginResponse { IsSuccess = false, Message = "아이디와 비밀번호를 입력해주세요." };
            }

            try
            {
                var payload = new { id = id, pw = pw };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{BaseUrl}/api/login", content);
                var responseJson = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(responseJson);

                if (response.IsSuccessStatusCode)
                {
                    return new LoginResponse { IsSuccess = true, Message = "로그인 성공!", Data = data };
                }
                else
                {
                    string detailMessage = data?.detail?.ToString() ?? "로그인 실패";
                    return new LoginResponse { IsSuccess = false, Message = detailMessage, Data = data };
                }
            }
            catch (HttpRequestException)
            {
                return new LoginResponse { IsSuccess = false, Message = "서버 통신 오류 (네트워크 문제 등)" };
            }
            catch (Exception)
            {
                return new LoginResponse { IsSuccess = false, Message = "알 수 없는 오류 발생" };
            }
        }

        // =========================================================
        // [2] 로그 리스트 조회
        // =========================================================
        public async Task<List<LogEntry>> GetLogsAsync(string date)
        {
            try
            {
                var payload = new { startDate = date };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                // [디버깅 로그] 요청 확인용
                Debug.WriteLine($"[API 요청] URL: {BaseUrl}/api/logs, Data: {JsonConvert.SerializeObject(payload)}");

                var response = await _client.PostAsync($"{BaseUrl}/api/logs", content);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var list = JsonConvert.DeserializeObject<List<ServerLogItem>>(json);

                    if (list == null) return new List<LogEntry>();

                    return list.Select(s => new LogEntry
                    {
                        MeasureId = s.mid,             // ID
                        Timestamp = s.timestamp,       // 시간
                        PropertyName = s.product_name, // 제품명
                        Status = (s.result == "NG" ? "불량" : "정상"), // 판정
                        DefectReason = (s.result == "NG" ? "불량 감지" : "-") // 사유(임시)
                    }).ToList();
                }
                else
                {
                    Debug.WriteLine($"[API 오류] 상태코드: {response.StatusCode}");
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Logs Error] {ex.Message}"); }
            return new List<LogEntry>();
        }

        // =========================================================
        // [3] 이미지 조회
        // =========================================================
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

        // =========================================================
        // [4] 측정 데이터 업로드
        // =========================================================
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

        // =========================================================
        // [5] 통계 데이터 조회
        // =========================================================
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
                    return JsonConvert.DeserializeObject<ServerStats>(json);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Stats Error] {ex.Message}"); }
            return null;
        }

        // =========================================================
        // [6] 시스템 제어
        // =========================================================
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

        // 내부 클래스 (JSON 파싱용)
        private class ServerLogItem
        {
            public int mid { get; set; }
            public string timestamp { get; set; }
            public string product_name { get; set; }
            public string result { get; set; }
        }
    }

    // 통계 모델
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