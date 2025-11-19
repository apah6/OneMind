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
        private int Category_ID; // 카테고리명
        private string _connStr = @"Server=localhost\SQLEXPRESS;Database=TestDB;Trusted_Connection=True;";
        private bool _recordOpened = false;
        private Recognize _recognizer;

        // 이미 출제된 제시어 관리 (종료 후 재시작하면 다시 나오게)
        private List<int> _usedQuestionIds = new List<int>();

        public Window1(Recognize recognizer, String teamName, int categoryName)
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

        // --- 새로 추가: 게임 타이머 정리 함수 ---
        private void DisposeGameTimer()
        {
            if (_timer != null)
            {
                try
                {
                    _timer.Stop();
                }
                catch { /* 무시 */ }

                _timer.Tick -= Timer_Tick;
                _timer = null;
            }
        }

        private void DisposeDetectTimer()
        {
            if (_detectTimer != null)
            {
                try
                {
                    _detectTimer.Stop();
                }
                catch { /* 무시 */ }

                _detectTimer.Tick -= CheckPlayersDetected;
                _detectTimer = null;
            }
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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // 윈도우가 닫힐 때 모든 타이머/이벤트 정리
            DisposeDetectTimer();
            DisposeGameTimer();

            if (_recognizer != null)
            {
                _recognizer.ColorHalvesUpdated -= Recognizer_ColorHalvesUpdated;
                try
                {
                    _recognizer.CloseKinect();
                }
                catch { /* 무시 */ }
            }
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
            _timer?.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_gameRunning) return;

            _timeLeft--;
            pgrTime.Value = 5 - _timeLeft;

            if (_timeLeft <= 0)
            {
                // 타이머 멈추고 정답 체크 흐름으로
                _timer?.Stop();
                _gameRunning = false;
                _timerStarted = false;

                FinishQuestion();
            }
        }

        private void GoToRecordWindow()
        {
            // 중복 실행 방지
            if (_recordOpened) return;
            _recordOpened = true;

            // 모든 타이머/이벤트 안전 정리
            DisposeDetectTimer();
            DisposeGameTimer();

            if (_recognizer != null)
            {
                _recognizer.ColorHalvesUpdated -= Recognizer_ColorHalvesUpdated;
                try
                {
                    _recognizer.CloseKinect();
                }
                catch { /* 무시 */ }
            }

            // 기록 창 열기
            Onemind_record record = new Onemind_record();
            record.Show();

            // 현재 창 닫기
            this.Close();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            // 버튼으로 중단 시에도 안전하게 정리
            DisposeGameTimer();
            DisposeDetectTimer();

            _gameRunning = false;
            _timerStarted = false;
            SaveScoreToDB(); // 점수 DB 저장    

            GoToRecordWindow(); // 기록 창으로 이동
        }

        private void FinishQuestion()
        {
            bool isCorrect = false;
            try
            {
                isCorrect = _recognizer.ComparePlayers(); // 같은 동작이면
            }
            catch
            {
                isCorrect = false;
            }

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
                    INSERT INTO GAME_RESULT (User_ID, Category_ID, Score, Play_Date)
                    VALUES (@team, @Category_ID, @score, GETDATE())
                    ";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@team", TeamName);
                    cmd.Parameters.AddWithValue("@score", _score);
                    cmd.Parameters.AddWithValue("@Category_ID", Category_ID);

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
            if (_currentQuestion >= _maxQuestions)
            {
                EndGame();
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_connStr))
                {
                    conn.Open();

                    string sql = @"
                SELECT TOP (@cnt) Game_Word_ID, Game_Word
                FROM GAME_WORD
                WHERE Category_ID = @categoryId
                ORDER BY NEWID()";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@cnt", 1);
                    cmd.Parameters.AddWithValue("@categoryId", Category_ID);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // 0번 컬럼 = Game_Word_ID (int)
                            int questionId = reader.GetInt32(0);

                            // 1번 컬럼 = Game_Word (string)
                            string questionText = reader.GetString(1);

                            _usedQuestionIds.Add(questionId);
                            lblKeyword.Content = questionText;

                            _timeLeft = 5;
                            pgrTime.Value = 0;
                            _timer?.Stop();
                            _timer?.Start();
                        }
                        else
                        {
                            EndGame();
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
