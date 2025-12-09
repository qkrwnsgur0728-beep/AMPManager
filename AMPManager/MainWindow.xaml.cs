using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
// using System.Windows.Input; // 모호함 방지를 위해 제거하고 아래에서 명시적 사용
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AMPManager.ViewModel;

namespace AMPManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // [수정됨] 키보드 입력 처리
        // WinForms와 WPF 라이브러리를 모두 사용 중이라 모호함이 발생하므로
        // 'System.Windows.Input.KeyEventArgs'라고 전체 경로를 적어줍니다.
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 1. 현재 DataContext가 MainViewModel인지 확인
            if (this.DataContext is MainViewModel mainVM)
            {
                // 2. 현재 보여지고 있는 화면이 HomeViewModel(메인화면)인지 확인
                if (mainVM.CurrentViewModel is HomeViewModel homeVM)
                {
                    // 3. 키 입력 분기
                    // 숫자 1 키 (상단 숫자키 또는 키패드) -> 불량 판정 시뮬레이션
                    if (e.Key == System.Windows.Input.Key.D1 || e.Key == System.Windows.Input.Key.NumPad1)
                    {
                        // true: 불량 +1, 전체 +1
                        homeVM.ManualInput(true);
                    }
                    // 숫자 2 키 -> 정상 판정 시뮬레이션
                    else if (e.Key == System.Windows.Input.Key.D2 || e.Key == System.Windows.Input.Key.NumPad2)
                    {
                        // false: 전체 +1 만 증가 (불량은 그대로)
                        homeVM.ManualInput(false);
                    }
                }
            }
        }
    }
}