using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AMPManager.Core;
using AMPManager.Model;

namespace AMPManager.ViewModel
{
    public class MainViewModel : ObservableObject
    {
        private BaseViewModel? _currentViewModel;
        private readonly Dictionary<string, BaseViewModel> _viewModels;

        // --- [기존] 가동 시간 타이머 (시스템 시작 시 작동) ---
        private DispatcherTimer _opTimer;
        private TimeSpan _opDuration;
        private string _operationTimeDisplay = "00:00:00";

        public string OperationTimeDisplay
        {
            get => _operationTimeDisplay;
            set => SetProperty(ref _operationTimeDisplay, value);
        }

        // --- [추가] 현재 시간 표시 (항상 작동) ---
        private string _currentTimeDisplay = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string CurrentTimeDisplay
        {
            get => _currentTimeDisplay;
            set => SetProperty(ref _currentTimeDisplay, value);
        }

        // --- 접속자 정보 ---
        public User CurrentUser { get; }
        public string UserRoleDisplay => CurrentUser.IsAdmin ? "👤 관리자 (Admin)" : "👤 일반 사원 (User)";

        // 관리자에게만 보이는 버튼
        public Visibility StatTabVisibility => CurrentUser.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

        // --- 커맨드 ---
        public ICommand NavigateCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand RestartCommand { get; }

        public BaseViewModel? CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public MainViewModel(User user)
        {
            CurrentUser = user;

            // 1. 가동 시간 타이머 초기화 (Start 버튼 누를 때만 감)
            _opTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _opTimer.Tick += (s, e) =>
            {
                _opDuration = _opDuration.Add(TimeSpan.FromSeconds(1));
                OperationTimeDisplay = _opDuration.ToString(@"hh\:mm\:ss");
            };

            // 2. [추가] 현재 시간 시계 타이머 (앱 켜지자마자 항상 감)
            var clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clockTimer.Tick += (s, e) =>
            {
                CurrentTimeDisplay = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            };
            clockTimer.Start();

            // 3. 뷰모델 생성
            var homeVM = new HomeViewModel();
            var logVM = new LogViewModel();
            var statVM = new StatisticsViewModel();
            var settingsVM = new SettingsViewModel();

            _viewModels = new Dictionary<string, BaseViewModel>
            {
                { "Main", homeVM },
                { "Log", logVM },
                { "Statistics", statVM },
                { "Settings", settingsVM }
            };

            // 4. 네비게이션
            NavigateCommand = new RelayCommand(o =>
            {
                if (o is string p && _viewModels.ContainsKey(p)) CurrentViewModel = _viewModels[p];
            });

            // 5. 시스템 제어 커맨드
            StartCommand = new RelayCommand(o =>
            {
                if (_viewModels["Main"] is HomeViewModel home)
                {
                    home.StartSimulation();
                    CurrentViewModel = home;
                    if (!_opTimer.IsEnabled) _opTimer.Start();
                }
            });

            RestartCommand = new RelayCommand(o =>
            {
                if (_viewModels["Main"] is HomeViewModel home)
                {
                    home.RestartSimulation();
                    if (!_opTimer.IsEnabled) _opTimer.Start();
                }
            });

            StopCommand = new RelayCommand(o =>
            {
                if (_viewModels["Main"] is HomeViewModel home)
                {
                    home.StopSimulation();
                    if (_opTimer.IsEnabled) _opTimer.Stop();
                }
            });

            // 6. 초기 화면 설정
            CurrentViewModel = _viewModels["Main"];
        }
    }
}