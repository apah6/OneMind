using System;
using System.Data.SqlClient;
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
        private int _currentQuestion = 0; // 현재 문제 번호
        private int _maxQuestions = 10; // 최대 문제 수  
        private int _score = 0; // 점수
        private string TeamName; // 팀명

        private Recognize _recognizer;


        public Window1(Recognize recognizer, String teamName)
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

            TeamName = teamName;

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
                _score = 0;
                lblScore.Dispatcher.Invoke(() =>
                {
                    lblScore.Content = $"점수: {_score} / {_maxQuestions}";
                });
            }
            else
            {
                lblKeyword.Content = $"게임 재개! 남은 시간: {_timeLeft}초";
                pgrTime.Value = 3 - _timeLeft;
                lblScore.Dispatcher.Invoke(() =>
                {
                    lblScore.Content = $"점수: {_score} / {_maxQuestions}";
                });
            }

            pgrTime.Maximum = 3;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_gameRunning)
            {
                return;
            }

            _timeLeft--;
            pgrTime.Value = 3 - _timeLeft;
            lblKeyword.Content = $"남은 시간: {_timeLeft}초";

            if (_timeLeft <= 0)
            {
                _timer.Stop();
                lblKeyword.Content = "시간 종료!";
                _gameRunning = false;
                _timerStarted = false;

                FinishQuestion(); // 문제 끝났다고 처리

            }
        }

        private void GoToRecordWindow()
        {
            Onemind_record record = new Onemind_record();

            record.Show();
            this.Close();  // 현재 Window1 닫기
            _recognizer.CloseKinect(); // Kinect 종료
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_timer != null && _timer.IsEnabled)
                _timer.Stop();

            _gameRunning = false;
            _timerStarted = false;
            SaveScoreToDB(); // 점수 DB 저장    

            GoToRecordWindow(); // 기록 창으로 이동
           

        }

        private void FinishQuestion()
        {
            bool isCorrect = _recognizer.ComparePlayers(); // 같은 동작인지 확인
            if (isCorrect)
            {
                _score++; // 정답 시 점수(1점) 추가
                lblKeyword.Content = "정답입니다! (+1점)";
            }
            else
            {
                 lblKeyword.Content = "오답입니다! (+0점)";
            }

            lblScore.Dispatcher.Invoke(() =>
            {
                lblScore.Content = $"점수: {_score} / {_maxQuestions}";
            });
            _currentQuestion++;

            if (_currentQuestion >= _maxQuestions) // 모든 문제 다 풀면
            {
                SaveScoreToDB(); // 점수 DB 저장
                GoToRecordWindow(); // 기록 창으로 이동
                return;
            }

            // 다음 문제 로딩
            LoadNextQuestion();
        }

        private void SaveScoreToDB() // 점수 DB 저장    
        {
            try
            {
                using (SqlConnection conn = new SqlConnection("서버=서버이름; 데이터베이스=DB이름; 사용자 ID=계정; 암호=비밀번호;"))
                {
                    conn.Open();

                    string sql = @"
                INSERT INTO RankingTable (TeamName, Score, RecordDate)
                VALUES (@team, @score, GETDATE())
            ";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@team", TeamName);
                    cmd.Parameters.AddWithValue("@score", _score);

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("점수 저장 오류: " + ex.Message);
            }
        }

        private void LoadNextQuestion() // DB 연결 필요
        {
            throw new NotImplementedException();
        }
    }
}
