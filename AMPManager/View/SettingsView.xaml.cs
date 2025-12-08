using System.Windows.Controls;

namespace AMPManager.View
{
    // [수정] 명시적으로 WPF의 UserControl임을 지정하여 모호함 해결
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }
    }
}