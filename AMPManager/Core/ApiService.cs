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
    public class LoginResponse
    {
        // JavaScript의 response.ok에 해당
        public bool IsSuccess { get; set; }
        // 서버에서 받은 상세 메시지 (성공 시 '로그인 성공', 실패 시 'data.detail')
        public string? Message { get; set; }
        // 선택 사항: 서버 응답 본문 전체를 담을 수도 있습니다.
        public object? Data { get; set; }
    }

    public class ApiService
    {
        private readonly HttpClient _client;

        // ★ 서버 주소 (Python 서버 IP와 포트 확인)
        private const string BaseUrl = "http://192.168.0.6:8000";

        public ApiService()
        {
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(5);
        }

        // [1] 로그인
        public async Task<LoginResponse> LoginAsync(string id, string pw)
        {
            // 1. 입력 유효성 검사
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(pw))
            {
                return new LoginResponse
                {
                    IsSuccess = false,
                    Message = "아이디와 비밀번호를 입력해주세요."
                };
            }

            try
            {
                // 2. 페이로드 생성 및 JSON 직렬화
                var payload = new { id = id, pw = pw };
                var content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                // 3. 서버에 POST 요청
                var response = await _client.PostAsync($"{BaseUrl}/api/login", content);

                // 4. 응답 본문 읽기
                var responseJson = await response.Content.ReadAsStringAsync();

                // 5. 응답 본문을 상세 데이터로 역직렬화
                var data = JsonConvert.DeserializeObject<dynamic>(responseJson);

                // 6. 응답 상태 코드 확인
                if (response.IsSuccessStatusCode) // 2xx 상태 코드
                {
                    return new LoginResponse
                    {
                        IsSuccess = true,
                        Message = "로그인 성공!",
                        Data = data
                    };
                }
                else // 4xx, 5xx 상태 코드 (로그인 실패)
                {
                    string detailMessage = data?.detail?.ToString() ?? "로그인 실패";

                    return new LoginResponse
                    {
                        IsSuccess = false,
                        Message = detailMessage,
                        Data = data
                    };
                }
            }
            catch (HttpRequestException)
            {
                return new LoginResponse
                {
                    IsSuccess = false,
                    Message = "서버 통신 오류 (네트워크 문제 등)"
                };
            }
            catch (Exception)
            {
                return new LoginResponse
                {
                    IsSuccess = false,
                    Message = "알 수 없는 오류 발생"
                };
            }
        }

        // [2] 로그 리스트 가져오기 (DB 조회) - ★수정된 부분★
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
                        // [수정 완료] Id(읽기전용) 대신 MeasureId에 값을 할당합니다.
                        MeasureId = s.mid,

                        Timestamp = s.timestamp,       // DB: measurement_time -> 화면: TIMESTAMP
                        PropertyName = s.product_name, // DB: product_name -> 화면: 제품명
                        Status = (s.result == "NG" ? "불량" : "정상") // DB: result -> 화면: 판정
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

                    // Base64 문자열을 이미지 바이트 배열로 변환
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

        // [6] 시스템 제어 (빈 함수 - 에러 방지용)
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
        public Dictionary<string, double> daily_rates { get; set; }
        public double avg_width { get; set; }
        public double avg_length { get; set; }
        public double avg_contour { get; set; }
        public double avg_center { get; set; }
    }
}