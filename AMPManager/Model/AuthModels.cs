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

    // 2. 로그인 응답 (받을 데이터) - [수정됨]
    public class LoginResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        // 서버에서 사용자 정보를 같이 보내준다고 가정 (없으면 null)
        [JsonProperty("user_name")]
        public string? UserName { get; set; }

        [JsonProperty("role")]
        public int? Role { get; set; } // 1: 관리자, 2: 일반

        // 로그인 실패 시 에러 메시지
        [JsonProperty("detail")]
        public string? ErrorMessage { get; set; }
    }
}