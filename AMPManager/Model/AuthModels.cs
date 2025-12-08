using Newtonsoft.Json;

namespace AMPManager.Model
{
    // 1. 로그인 요청
    public class LoginRequest
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("pw")]
        public string Pw { get; set; } = string.Empty;
    }

    // 2. [수정됨] 회원가입 요청 (서버의 UserSignup 모델과 일치시킴)
    public class SignupRequest
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("pw")]
        public string Pw { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("role")]
        public int Role { get; set; } = 2; // 기본값 2 (User)
    }

    // 3. 응답 (로그인/회원가입 공통)
    public class LoginResponse
    {
        // 서버 응답: { "code": 200, "message": "...", ... }
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("user_name")]
        public string UserName { get; set; } = "Unknown";

        [JsonProperty("role")]
        public int Role { get; set; } = 2;
    }
}