using System;
using System.Windows;
using System.Windows.Threading;


namespace OneMind
{
    public partial class Window1 : Window
    {
        private DispatcherTimer _timer;
        private DispatcherTimer _detectTimer; // 플레이어 감지용 타이머
        private int _timeLeft = 3;
        private bool _gameRunning = false;
        private bool _timerStarted = false;


        private Recognize _recognizer;

        public Window1(Recognize recognizer)
        {
            InitializeComponent();
            InitializeDetectionCheck();

            _recognizer = recognizer;

            if (_recognizer != null)
            {
                // Player 1, Player 2 모두 컬러 영상 연결
                imgPlayer1.Source = _recognizer.ColorBitmap;
                imgPlayer2.Source = _recognizer.ColorBitmap;

            }

            InitializeTimer();
        }

        private void InitializeDetectionCheck()
        {
            _detectTimer = new DispatcherTimer();
            _detectTimer.Interval = TimeSpan.FromMilliseconds(500); // 0.5초마다 인식여부 체크
            _detectTimer.Tick += CheckPlayersDetected;
            _detectTimer.Start();
        }

        private void CheckPlayersDetected(object sender, EventArgs e)
        {
            bool player1 = _recognizer.IsPlayer1Detected();
            bool player2 = _recognizer.IsPlayer2Detected();

            lblPerceive1.Content = player1 ? "Player1 인식됨" : "대기 중...";
            lblPerceive2.Content = player2 ? "Player2 인식됨" : "대기 중...";

            // 두 명 모두 인식되면 게임 시작
            if (!_timerStarted && player1 && player2)
            {
                _timerStarted = true;
                StartGame(false);
            }

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

               // GoToRecordWindow(); // 자동으로 기록 창으로 이동
            }
        }

        private void GoToRecordWindow()
        {
            Onemind_record record = new Onemind_record();

            record.Show();
            this.Close();  // 현재 Window1 닫기
        }


        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_timer != null && _timer.IsEnabled)
                _timer.Stop();

            _gameRunning = false;
            _timerStarted = false;

            GoToRecordWindow(); // 기록 창으로 이동

        }


    }
}
