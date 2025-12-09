using System.Windows;
using AMPManager.ViewModel;

namespace AMPManager.View
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            var vm = new LoginViewModel();

            // 뷰모델의 "닫기" 신호가 오면 실제로 창을 닫고 DialogResult를 true로 설정
            vm.CloseAction = () =>
            {
                try
                {
                    // ShowDialog()로 열렸을 때만 작동
                    this.DialogResult = true;
                }
                catch (InvalidOperationException)
                {
                    // Show()로 열렸으면 예외 발생 -> 그냥 무시하고 닫기 진행
                }
                this.Close();
            };

            this.DataContext = vm;
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}