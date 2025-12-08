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

                // [중요] 기존 백도어 기능 유지 (서버 연결 안될 때 비상용)
                if (InputId == "ID1234" && pw == "PW1234")
                {
                    LoggedInUser = new User("관리자(Local)", "ID1234", 1); // 1: 관리자
                    CloseAction?.Invoke();
                    return;
                }

                // [수정] 서버 로그인 시도
                User? serverUser = await _apiService.LoginAsync(InputId, pw);

                if (serverUser != null)
                {
                    // 로그인 성공! (서버 정보를 그대로 사용)
                    LoggedInUser = serverUser;
                    CloseAction?.Invoke();
                }
                else
                {
                    // 로그인 실패 (모호함 방지를 위해 전체 네임스페이스 사용)
                    System.Windows.MessageBox.Show(
                        "아이디 또는 비밀번호가 틀렸습니다.\n(서버 상태를 확인해주세요)",
                        "로그인 실패",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            });
        }
    }
}