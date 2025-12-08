using AMPManager.Core;

namespace AMPManager.ViewModel
{
    public class SettingsViewModel : BaseViewModel
    {
        // 예시 설정: 불량 판정 임계값
        private double _threshold = 85.0;
        public double Threshold
        {
            get => _threshold;
            set => SetProperty(ref _threshold, value);
        }

        public SettingsViewModel()
        {
            // 초기화 로직
        }
    }
}