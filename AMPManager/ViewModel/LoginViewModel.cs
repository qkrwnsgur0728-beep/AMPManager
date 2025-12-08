using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AMPManager.Core;
using AMPManager.Model;

namespace AMPManager.ViewModel
{
    public class LoginViewModel : ObservableObject
    {
        private ApiService _apiService = new ApiService();

        private string _inputId = "";
        public string InputId { get => _inputId; set => SetProperty(ref _inputId, value); }

        public User? LoggedInUser { get; private set; }
        public ICommand LoginCommand { get; }
        public Action? CloseAction { get; set; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(async o =>
            {
                var passwordBox = o as PasswordBox;
                string pw = passwordBox != null ? passwordBox.Password : "";

                // [비상용 백도어] 서버 연결 실패 시 로컬 테스트용
                if (InputId == "ID1234" && pw == "PW1234")
                {
                    LoggedInUser = new User("비상관리자", "ID1234", 1); // 1: 관리자
                    CloseAction?.Invoke();
                    return;
                }

                // [수정] 서버 API 호출 및 결과 처리
                User? serverUser = await _apiService.LoginAsync(InputId, pw);

                if (serverUser != null)
                {
                    // 로그인 성공: 서버가 준 정보를 그대로 사용
                    LoggedInUser = serverUser;

                    // (옵션) 환영 메시지
                    // MessageBox.Show($"{serverUser.Name}님 환영합니다!", "로그인 성공");

                    CloseAction?.Invoke(); // 메인 화면으로 이동
                }
                else
                {
                    // 로그인 실패
                    System.Windows.MessageBox.Show("아이디 또는 비밀번호가 틀렸거나 서버에 연결할 수 없습니다.",
                                    "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
    }
}