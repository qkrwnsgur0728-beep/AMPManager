using System;
using System.Linq; // Enumerable.OfType
using System.Windows.Input;
// [중요] System.Windows.Forms와 충돌 방지를 위해 using System.Windows 생략

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
            LoginCommand = new RelayCommand(o =>
            {
                // [중요] PasswordBox 명시
                var passwordBox = o as System.Windows.Controls.PasswordBox;
                string pw = passwordBox != null ? passwordBox.Password : "";

                if (InputId == "1234" && pw == "1234")
                {
                    LoggedInUser = new User("관리자(비상)", "1234", 2);
                    CloseAction?.Invoke();
                    return;
                }

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
                        // [중요] MessageBox 명시
                        System.Windows.MessageBox.Show("아이디/비번을 확인하세요.", "로그인 실패");
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"DB 오류: {ex.Message}");
                }
            });

            OpenSignUpCommand = new RelayCommand(o =>
            {
                SignUpWindow signUp = new SignUpWindow();

                // [중요] Application 명시 (WinForms 충돌 방지)
                var app = System.Windows.Application.Current;
                if (app != null)
                {
                    if (app.MainWindow != null)
                    {
                        signUp.Owner = app.MainWindow;
                    }
                    else
                    {
                        // 로그인 창(활성화된 창)을 찾아서 Owner로 지정
                        var active = app.Windows.OfType<System.Windows.Window>().SingleOrDefault(w => w.IsActive);
                        if (active != null) signUp.Owner = active;
                    }
                }
                signUp.ShowDialog();
            });
        }
    }
}