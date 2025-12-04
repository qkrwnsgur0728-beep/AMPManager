using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AMPManager.Core;

namespace AMPManager.ViewModel
{
    public class SignUpViewModel : ObservableObject
    {
        private DatabaseManager _dbManager = new DatabaseManager();

        public string InputId { get; set; } = "";
        public string InputName { get; set; } = "";
        public string InputAdminCode { get; set; } = ""; // 관리자 인증 코드

        public ICommand RegisterCommand { get; }
        public Action? CloseAction { get; set; }

        public SignUpViewModel()
        {
            RegisterCommand = new RelayCommand(o =>
            {
                var passwordBox = o as PasswordBox;
                string pw = passwordBox != null ? passwordBox.Password : "";

                // 1. 입력 확인
                if (string.IsNullOrWhiteSpace(InputId) || string.IsNullOrWhiteSpace(pw) || string.IsNullOrWhiteSpace(InputName))
                {
                    System.Windows.MessageBox.Show("모든 정보를 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. 권한 설정 (관리자 코드 '1234' 입력 시 관리자 권한 부여)
                int role = 1; // 기본: 일반 사용자 (Role 1)

                if (!string.IsNullOrEmpty(InputAdminCode))
                {
                    if (InputAdminCode == "1234")
                    {
                        role = 2; // 관리자 (Role 2 - DB 기준)
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("잘못된 관리자 코드입니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 3. DB 등록
                bool success = _dbManager.RegisterUser(InputId, pw, InputName, role);

                if (success)
                {
                    System.Windows.MessageBox.Show("회원가입 성공! 로그인해주세요.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    CloseAction?.Invoke();
                }
                else
                {
                    System.Windows.MessageBox.Show("회원가입 실패. (아이디 중복 등)", "실패", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
    }
}