using System; // InvalidOperationException 처리를 위해 필요
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

            // ★ 에러 방지 코드 적용됨
            vm.CloseAction = () =>
            {
                try
                {
                    // 모달 창(ShowDialog)일 때만 작동
                    this.DialogResult = true;
                }
                catch (InvalidOperationException)
                {
                    // 일반 창(Show)일 때 발생하는 에러 무시
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