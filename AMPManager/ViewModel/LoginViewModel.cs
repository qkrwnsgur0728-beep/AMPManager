using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using AMPManager.Core;
using AMPManager.Model;

namespace AMPManager.ViewModel
{
    public class LoginViewModel : ObservableObject
    {
        private ApiService _apiService = new ApiService();

        private string _inputId = "";
        public string InputId { get => _inputId; set => SetProperty(ref _inputId, value); }

        private string _errorMessage = "";
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        public User? LoggedInUser { get; private set; }
        public ICommand LoginCommand { get; }
        public Action? CloseAction { get; set; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(async o =>
            {
                var passwordBox = o as PasswordBox;
                string pw = passwordBox != null ? passwordBox.Password : "";
                ErrorMessage = "";

                User? user = null;

                // [1] 로컬 관리자 계정 체크 (서버 통신 없이 즉시 로그인)
                // ★ 비상용 계정: ID="admin", PW="1234"
                if (InputId == "admin" && pw == "1234")
                {
                    // 로컬 관리자 생성 (Role=1: 관리자)
                    user = new User("로컬 관리자", "admin", 2);
                }
                else
                {
                    // [2] 로컬 계정이 아니면 서버 API를 통해 로그인 시도
                    user = await _apiService.LoginAsync(InputId, pw);
                }

                // 결과 처리
                if (user != null)
                {
                    LoggedInUser = user;
                    CloseAction?.Invoke(); // 로그인 창 닫고 메인 이동
                }
                else
                {
                    ErrorMessage = "로그인 실패: 아이디/비밀번호를 확인하거나 서버 상태를 점검하세요.";
                    System.Windows.MessageBox.Show(ErrorMessage, "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
    }
}