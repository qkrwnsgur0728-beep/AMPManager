using Newtonsoft.Json;

namespace AMPManager.Model
{
    // 1. 로그인 요청 (보낼 데이터)
    public class LoginRequest
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("pw")]
        public string Pw { get; set; } = string.Empty;
    }

    // 2. 로그인 응답 (받을 데이터)
    public class LoginResponse
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        // [추가] 서버에서 보내주는 사용자 정보 매핑
        // 서버가 이 필드를 안 보내줄 수도 있으므로 기본값 설정
        [JsonProperty("user_name")]
        public string UserName { get; set; } = "Unknown";

        [JsonProperty("role")]
        public int Role { get; set; } = 2; // 기본값: 2 (일반 사용자)
    }
}