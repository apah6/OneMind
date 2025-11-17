using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Kinect;
using System.Windows.Media.Imaging;

namespace OneMind
{
    public partial class Window1 : Window
    {
        private DispatcherTimer _timer;
        private int _timeLeft = 3;
        private bool _gameRunning = false;
        private bool _timerStarted = false;

        private Recognize _recognizer;

        public Window1(Recognize recognizer)
        {
            InitializeComponent();

            _recognizer = recognizer;

            if (_recognizer != null)
            {
                // Player 1, Player 2 모두 컬러 영상 연결
                imgPlayer1.Source = _recognizer.ColorBitmap;
                imgPlayer2.Source = _recognizer.ColorBitmap;



                _recognizer.Skeletons = new Skeleton[_recognizer.Sensor.SkeletonStream.FrameSkeletonArrayLength];

            }

            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }

        private void StartGame(bool isResume = false)
        {
            _gameRunning = true;

            if (!isResume)
            {
                _timeLeft = 3;
                pgrTime.Value = 0;
                lblKeyword.Content = "게임 시작!";
            }
            else
            {
                lblKeyword.Content = $"게임 재개! 남은 시간: {_timeLeft}초";
                pgrTime.Value = 3 - _timeLeft;
            }

            pgrTime.Maximum = 3;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_gameRunning) return;

            _timeLeft--;
            pgrTime.Value = 3 - _timeLeft;
            lblKeyword.Content = $"남은 시간: {_timeLeft}초";

            if (_timeLeft <= 0)
            {
                _timer.Stop();
                lblKeyword.Content = "시간 종료!";
                _gameRunning = false;
                _timerStarted = false;
            }
        }


        //private void Sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        //{
        //    using (SkeletonFrame frame = e.OpenSkeletonFrame())
        //    {
        //        if (frame == null) return;

        //        Skeleton[] skeletons = new Skeleton[frame.SkeletonArrayLength];
        //        frame.CopySkeletonDataTo(skeletons);

        //        bool player1Detected = false;
        //        bool player2Detected = false;
        //        int trackedCount = 0;

        //        foreach (Skeleton skeleton in skeletons)
        //        {
        //            if (skeleton.TrackingState != SkeletonTrackingState.Tracked) continue;

        //            if (trackedCount == 0) player1Detected = true;
        //            else if (trackedCount == 1) player2Detected = true;

        //            trackedCount++;
        //        }

        //        // UI 업데이트
        //        lblPerceive1.Dispatcher.Invoke(() =>
        //            lblPerceive1.Content = player1Detected ? "Player1 인식됨" : "대기 중...");
        //        lblPerceive2.Dispatcher.Invoke(() =>
        //            lblPerceive2.Content = player2Detected ? "Player2 인식됨" : "대기 중...");

        //        // 두 명 감지 시 타이머 시작
        //        if (!_timerStarted && player1Detected && player2Detected)
        //        {
        //            _timerStarted = true;
        //            StartGame(false);
        //        }
        //    }
        //}

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_timer != null && _timer.IsEnabled)
                _timer.Stop();

            _gameRunning = false;
            _timerStarted = false;
            lblKeyword.Content = "게임이 중단되었습니다.";
        }

        //protected override void OnClosed(EventArgs e)
        //{
        //    if (_recognizer?.Sensor != null)
        //    {
        //        _recognizer.Sensor.SkeletonFrameReady -= Sensor_SkeletonFrameReady;
        //    }

        //    if (_timer != null && _timer.IsEnabled)
        //        _timer.Stop();
        //}
    }
}
