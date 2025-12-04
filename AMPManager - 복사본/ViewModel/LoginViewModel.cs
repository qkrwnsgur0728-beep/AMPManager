using System;
using System.Windows; // 여기서도 System.Windows.MessageBox 등을 쓰지만, Application은 명시적으로 지정합니다.
using System.Windows.Controls;
using System.Windows.Input;
using AMPManager.Core;
using AMPManager.Model;
using AMPManager.View;

namespace AMPManager.ViewModel
{
    public class LoginViewModel : ObservableObject
    {
        private DatabaseManager _dbManager = new DatabaseManager();

        private string _inputId = "";
        public string InputId { get => _inputId; set => SetProperty(ref _inputId, value); }

        public User? LoggedInUser { get; private set; }

        public ICommand LoginCommand { get; }
        public ICommand OpenSignUpCommand { get; }

        public Action? CloseAction { get; set; }

        public LoginViewModel()
        {
            // 로그인 로직
            LoginCommand = new RelayCommand(o =>
            {
                var passwordBox = o as PasswordBox;
                string pw = passwordBox != null ? passwordBox.Password : "";

                // 1. 비상용 마스터 계정 (1234/1234)
                if (InputId == "1234" && pw == "1234")
                {
                    LoggedInUser = new User("관리자(비상)", "1234", 2);
                    CloseAction?.Invoke();
                    return;
                }

                // 2. DB 계정 로그인
                try
                {
                    User? dbUser = _dbManager.Login(InputId, pw);
                    if (dbUser != null)
                    {
                        LoggedInUser = dbUser;
                        CloseAction?.Invoke();
                    }
                    else
                    {
                        // 명시적으로 System.Windows.MessageBox 사용 (WinForms와 충돌 방지)
                        System.Windows.MessageBox.Show("아이디 또는 비밀번호가 틀렸습니다.", "로그인 실패", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"DB 오류: {ex.Message}", "에러", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            });

            // ★ 회원가입 버튼 클릭 시 실행 (수정된 부분)
            OpenSignUpCommand = new RelayCommand(o =>
            {
                SignUpWindow signUp = new SignUpWindow();

                // ★★★ [수정] Application -> System.Windows.Application 으로 명시 ★★★
                if (System.Windows.Application.Current.MainWindow != null)
                {
                    signUp.Owner = System.Windows.Application.Current.MainWindow;
                }

                signUp.ShowDialog();
            });
        }
    }
}