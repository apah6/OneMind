using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Threading;



namespace OneMind
{
    public partial class Window1 : Window
    {
        private DispatcherTimer _timer;
        private DispatcherTimer _detectTimer; // 플레이어 감지용 타이머
        private int _timeLeft = 5;
        private bool _gameRunning = false;
        private bool _timerStarted = false;
        private int _currentQuestion = 0; // 현재 문제 번호
        private int _maxQuestions = 10; // 최대 문제 수  
        private int _score = 0; // 점수
        private string TeamName; // 팀명
        private string Category_ID; // 카테고리명
        private string _connStr = @"Server=localhost\SQLEXPRESS;Database=TestDB;Trusted_Connection=True;";

        private Recognize _recognizer;

        // 이미 출제된 제시어 관리 (종료 후 재시작하면 다시 나오게)
        private List<int> _usedQuestionIds = new List<int>();


        public Window1(Recognize recognizer, String teamName, string categoryName)
        {
            InitializeComponent();
            InitializeDetectionCheck();

            _recognizer = recognizer;

            if (_recognizer != null)
            {

                _recognizer.ColorHalvesUpdated += Recognizer_ColorHalvesUpdated;

            }
            InitializeTimer();
            TeamName = teamName;
            this.Category_ID = categoryName;
        }

        private void Recognizer_ColorHalvesUpdated(System.Windows.Media.Imaging.WriteableBitmap left, System.Windows.Media.Imaging.WriteableBitmap right)
        {
            // 이벤트가 UI 스레드에서 호출되지 않을 수 있으므로 안전하게 Dispatcher 사용
            Dispatcher.BeginInvoke(new Action(() =>
            {
                imgPlayer1.Source = left;
                imgPlayer2.Source = right;
            }));

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
                LoadNextQuestion(); // 첫 문제 즉시 출력
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
                _timeLeft = 5;
                pgrTime.Value = 0;
                lblKeyword.Content = "게임 시작!";
                _score = 0;
                lblScore.Dispatcher.Invoke(() =>
                {
                    lblScore.Content = $"{_score} / {_maxQuestions}";
                });
            }
            else
            {
                lblKeyword.Content = $"게임 재개!";
                pgrTime.Value = 5 - _timeLeft;
                lblScore.Dispatcher.Invoke(() =>
                {
                    lblScore.Content = $"{_score} / {_maxQuestions}";
                });
            }

            pgrTime.Maximum = 5;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_gameRunning)
            {
                return;
            }

            _timeLeft--;
            pgrTime.Value = 5 - _timeLeft;

            if (_timeLeft <= 0)
            {
                _timer.Stop();
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
            {
                _timer.Stop();
            }

            _gameRunning = false;
            _timerStarted = false;
            SaveScoreToDB(); // 점수 DB 저장    

            GoToRecordWindow(); // 기록 창으로 이동


        }

        private void FinishQuestion()
        {
            bool isCorrect = _recognizer.ComparePlayers(); // 같은 동작이면

            if (isCorrect)
            {

                _score++;
            }

            lblKeyword.Content = isCorrect ? "정답입니다! (+1점)" : "오답입니다! (+0점)";
            lblScore.Content = $"{_score} / {_maxQuestions}";
            _currentQuestion++;

            if (_currentQuestion >= _maxQuestions) // 모든 문제 완료
            {
                EndGame();
                return;
            }

            // 1초 후 다음 문제
            DispatcherTimer delayTimer = new DispatcherTimer();
            delayTimer.Interval = TimeSpan.FromSeconds(1);
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                LoadNextQuestion();
            };
            delayTimer.Start();

        }

        private void EndGame()
        {
            _gameRunning = false;
            _timerStarted = false;
            SaveScoreToDB();
            GoToRecordWindow();
        }

        private void SaveScoreToDB() // 점수 DB 저장    
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connStr))
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

        private void LoadNextQuestion()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connStr))
                {
                    conn.Open();

                    string sql = @"
                    SELECT TOP (@cnt) Game_Word
                    FROM GAME_WORD
                    WHERE Category_ID = @categoryId
                    ORDER BY NEWID()";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@cnt", 1); // 한 번에 1문제
                    cmd.Parameters.AddWithValue("@categoryId", Category_ID);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int questionId = reader.GetInt32(0);
                            string questionText = reader.GetString(1);

                            _usedQuestionIds.Add(questionId); // 출제된 문제 ID 추가
                            lblKeyword.Content = questionText;

                            _timeLeft = 5;  // 5초 행동 시간
                            pgrTime.Value = 0;
                            _timer.Stop();
                            _timer.Start();
                        }
                        else
                        {
                            EndGame(); // 더 이상 문제가 없으면 게임 종료
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("문제 로딩 오류: " + ex.Message);
            }
        }
    }
}