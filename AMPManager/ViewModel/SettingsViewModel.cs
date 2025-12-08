using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AMPManager.Core;

namespace AMPManager.ViewModel
{
    // MainViewModel 딕셔너리 오류 방지를 위해 BaseViewModel 상속 유지
    public class SettingsViewModel : BaseViewModel
    {
        private ApiService _apiService = new ApiService();

        private string _inputId = "";
        public string InputId
        {
            get => _inputId;
            set => SetProperty(ref _inputId, value);
        }

        private string _inputName = "";
        public string InputName
        {
            get => _inputName;
            set => SetProperty(ref _inputName, value);
        }

        public ICommand SignupCommand { get; }

        public SettingsViewModel()
        {
            SignupCommand = new RelayCommand(async o =>
            {
                var passwordBox = o as PasswordBox;
                string inputPw = passwordBox != null ? passwordBox.Password : "";

                // 1. 입력 확인
                if (string.IsNullOrWhiteSpace(InputId) ||
                    string.IsNullOrWhiteSpace(inputPw) ||
                    string.IsNullOrWhiteSpace(InputName))
                {
                    System.Windows.MessageBox.Show("모든 필드를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. 서버 전송 (role = 2: 일반 사용자)
                bool isSuccess = await _apiService.SignupAsync(InputId, inputPw, InputName, 2);

                // 3. 결과 처리
                if (isSuccess)
                {
                    System.Windows.MessageBox.Show("회원가입이 완료되었습니다!", "성공", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 입력창 초기화
                    InputId = "";
                    InputName = "";
                    if (passwordBox != null) passwordBox.Password = "";
                }
                else
                {
                    System.Windows.MessageBox.Show("회원가입 실패\n(이미 존재하는 아이디입니다.)", "실패", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
    }
}