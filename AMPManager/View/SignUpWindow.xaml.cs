using System.Windows;
using AMPManager.ViewModel;

namespace AMPManager.View
{
    public partial class SignUpWindow : Window
    {
        public SignUpWindow()
        {
            InitializeComponent();
            var vm = new SignUpViewModel();
            vm.CloseAction = () => this.Close();
            this.DataContext = vm;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}