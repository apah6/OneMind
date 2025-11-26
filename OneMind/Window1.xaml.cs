using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Timers;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OneMind
{
    public partial class Window1 : Window
    {
        private Timer _logicTimer;
        private DispatcherTimer _detectTimer; // 플레이어 감지 타이머
        private List<DispatcherTimer> _tempTimers = new List<DispatcherTimer>();

        private int _timeLeftTicks = 30; // 0.1초 단위 (3초)
        private const int MaxTicks = 30;
         
        private bool _gameRunning = false; // 게임 진행 상태
        private bool _gameInitialized = false; // 게임 초기화 상태
        private int _currentQuestion = 0; // 현재 문제 번호
        private int _maxQuestions = 10; //  최대 문제 수
        private bool _lastCorrect = false; // 마지막 정답 여부
        private int _score = 0; // 점수
        private string TeamName; // 팀 이름
        private int Category_ID; // 카테고리 ID
        private string _connStr = @"Server=localhost\SQLEXPRESS;Database=TestDB;Trusted_Connection=True;";
        private bool _recordOpened = false;
        private Recognize _recognizer;

        // / 이미 출제된 제시어 관리 (종료 후 재시작하면 다시 나오게)
        private List<int> _usedQuestionIds = new List<int>();
        private int _currentQuestionId; // // 현재 문제의 ID 저장 (중복 출제 방지)
        private string _currentQuestionText; 

        private WriteableBitmap _leftBitmap;
        private WriteableBitmap _rightBitmap;
        private byte[] _leftPixels;
        private byte[] _rightPixels;

        public Window1(Recognize recognizer, string teamName, int categoryName)
        {
            InitializeComponent();
            InitializeDetectionCheck();

            _recognizer = recognizer;
            if (_recognizer != null)
                _recognizer.ColorHalvesUpdated += Recognizer_ColorHalvesUpdated;

            TeamName = teamName;
            Category_ID = categoryName;
        }

        // Kinect 영상 처리
        private void Recognizer_ColorHalvesUpdated(WriteableBitmap leftFrame, WriteableBitmap rightFrame)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                if (_leftBitmap == null || _rightBitmap == null)
                {
                    _leftBitmap = new WriteableBitmap(leftFrame.PixelWidth, leftFrame.PixelHeight, leftFrame.DpiX, leftFrame.DpiY, leftFrame.Format, null);
                    _rightBitmap = new WriteableBitmap(rightFrame.PixelWidth, rightFrame.PixelHeight, rightFrame.DpiX, rightFrame.DpiY, rightFrame.Format, null);
                    imgPlayer1.Source = _leftBitmap;
                    imgPlayer2.Source = _rightBitmap;

                    _leftPixels = new byte[leftFrame.PixelHeight * leftFrame.PixelWidth * (leftFrame.Format.BitsPerPixel / 8)];
                    _rightPixels = new byte[rightFrame.PixelHeight * rightFrame.PixelWidth * (rightFrame.Format.BitsPerPixel / 8)];
                }

                int strideLeft = leftFrame.PixelWidth * (leftFrame.Format.BitsPerPixel / 8);
                int strideRight = rightFrame.PixelWidth * (rightFrame.Format.BitsPerPixel / 8);

                leftFrame.CopyPixels(_leftPixels, strideLeft, 0);
                rightFrame.CopyPixels(_rightPixels, strideRight, 0);

                _leftBitmap.Lock();
                _leftBitmap.WritePixels(new Int32Rect(0, 0, leftFrame.PixelWidth, leftFrame.PixelHeight), _leftPixels, strideLeft, 0);
                _leftBitmap.AddDirtyRect(new Int32Rect(0, 0, leftFrame.PixelWidth, leftFrame.PixelHeight));
                _leftBitmap.Unlock();

                _rightBitmap.Lock();
                _rightBitmap.WritePixels(new Int32Rect(0, 0, rightFrame.PixelWidth, rightFrame.PixelHeight), _rightPixels, strideRight, 0);
                _rightBitmap.AddDirtyRect(new Int32Rect(0, 0, rightFrame.PixelWidth, rightFrame.PixelHeight));
                _rightBitmap.Unlock();
            }));
        }


        // 플레이어 감지
        private void InitializeDetectionCheck()
        {
            _detectTimer = new DispatcherTimer();
            _detectTimer.Interval = TimeSpan.FromMilliseconds(500);
            _detectTimer.Tick += CheckPlayersDetected;
            _detectTimer.Start();
        }

        private void CheckPlayersDetected(object sender, EventArgs e)
        {
            if (_recognizer == null)
            {
                return;
            }

            bool player1 = _recognizer.IsPlayer1Detected(); // 플레이어 1 감지 여부
            bool player2 = _recognizer.IsPlayer2Detected(); // 플레이어 2 감지 여부

            lblPerceive1.Content = player1 ? "Player1 인식됨" : "대기 중..."; // 플레이어 1 상태 업데이트
            lblPerceive2.Content = player2 ? "Player2 인식됨" : "대기 중..."; // 플레이어 2 상태 업데이트

            if (!_gameInitialized && player1 && player2) // 두 플레이어가 모두 감지되면 게임 시작
            {
                _gameInitialized = true;
                StartGame();
                LoadNextQuestion();
            }
            else
            {
                ResumeTimerIfPlayersDetected(); // 플레이어가 감지되면 타이머 재개
            }
        }

        private void ResumeTimerIfPlayersDetected()
        {
            if (!_gameRunning || _timeLeftTicks <= 0) // 게임이 진행 중이 아니거나 시간이 다 된 경우
            {
                return;
            }

            if (_recognizer.IsPlayer1Detected() && _recognizer.IsPlayer2Detected()) // 두 플레이어가 모두 감지되면 타이머 재개
            {
                lblKeyword.Content = _currentQuestionText ?? "게임 재개!";
                if (_logicTimer != null && !_logicTimer.Enabled) // 타이머가 멈춰있다면 재개
                    _logicTimer.Start(); // 타이머 재개
            }
            else
            {
                _logicTimer?.Stop(); // 타이머 일시정지
                lblKeyword.Content = "플레이어 대기 중...";
            }
        }


        // 게임 시작/타이머
        private void StartGame()
        {
            _gameRunning = true; // 게임 진행 상태로 변경
            _timeLeftTicks = MaxTicks; // 시간 초기화

            lblKeyword.Content = "게임 시작!";
            _score = 0;
            _currentQuestion = 0;
            lblScore.Content = $"{_score} / {_maxQuestions}"; 
        }

        private void StartLogicTimer()
        {
            if (_logicTimer != null) // 기존 타이머가 있으면 정리
            {
                _logicTimer.Stop();
                _logicTimer.Dispose();
            }

            _logicTimer = new Timer(100);
            _logicTimer.Elapsed += (s, e) =>
            {
                if (!_recognizer.IsPlayer1Detected() || !_recognizer.IsPlayer2Detected()) // 플레이어가 감지되지 않으면 타이머 일시정지
                {
                    return;
                }

                _timeLeftTicks--; // 감지되면 시간 감소

                try { _lastCorrect = _recognizer.ComparePlayers(); } catch { }

                if (_timeLeftTicks <= 0) // 시간이 다 되었을 때
                {
                    _logicTimer.Stop(); // 타이머 정지
                    if (_lastCorrect) 
                    {
                        _score++;
                    }
                    Dispatcher.Invoke(() => FinishQuestion());
                }
            };
            _logicTimer.Start();
        }


        // 문제 처리
        private void LoadNextQuestion()
        {
            if (_currentQuestion >= _maxQuestions) // 최대 문제 수 도달 시 게임 종료
            {
                EndGame();
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_connStr))
                {
                    conn.Open();
                    string notInClause = _usedQuestionIds.Count > 0 ? string.Join(",", _usedQuestionIds) : "0";

                    string sql = $@"
                        SELECT TOP 1 Word_ID, Game_Word
                        FROM GAME_WORD
                        WHERE Category_ID = @categoryId 
                          AND Word_ID NOT IN ({notInClause})
                        ORDER BY NEWID()";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@categoryId", Category_ID);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            _currentQuestionId = reader.GetInt32(0);
                            _currentQuestionText = reader.GetString(1);
                            lblKeyword.Content = _currentQuestionText;

                            _timeLeftTicks = MaxTicks;

                            // ProgressBar 초기화
                            pgrTime.Minimum = 0;
                            pgrTime.Maximum = 1;
                            pgrTime.Value = 0;

                            DoubleAnimation anim = new DoubleAnimation
                            {
                                From = 0,
                                To = 1,
                                Duration = TimeSpan.FromSeconds(3)
                            };
                            pgrTime.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, anim);

                            StartLogicTimer();
                        }
                        else
                        {
                            lblKeyword.Content = "문제를 다 풀었습니다.";
                            DispatcherTimer finalDelayTimer = new DispatcherTimer();
                            finalDelayTimer.Interval = TimeSpan.FromSeconds(2);
                            finalDelayTimer.Tick += (s, e) =>
                            {
                                finalDelayTimer.Stop();
                                EndGame();
                            };
                            finalDelayTimer.Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("문제 로딩 오류: " + ex.Message);
            }
        }

        private void FinishQuestion()
        {
            _gameRunning = false; // 문제 종료 상태로 변경

            if (_currentQuestionId != 0 && !_usedQuestionIds.Contains(_currentQuestionId)) // 중복 출제 방지
            {
                _usedQuestionIds.Add(_currentQuestionId);
            }

            _currentQuestion++;
            lblScore.Content = $"{_score} / {_maxQuestions}"; // 점수 업데이트

            lblKeyword.Content = _lastCorrect ? "정답입니다! (+1점)" : "오답입니다! (+0점)";

            DispatcherTimer delayTimer = new DispatcherTimer();
            delayTimer.Interval = TimeSpan.FromSeconds(1);
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                LoadNextQuestion();
                _tempTimers.Remove(delayTimer);
            };
            _tempTimers.Add(delayTimer);
            delayTimer.Start();
        }


        // 게임 종료/점수 저장
        private void EndGame()
        {
            _gameRunning = false;
            lblKeyword.Content = "게임 종료! 점수 기록 중...";
            SaveScoreToDB();
            GoToRecordWindow();
        }

        private void SaveScoreToDB()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connStr))
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO GAME_RESULT (User_ID, Category_ID, Score, Play_Date)
                        VALUES (@team, @Category_ID, @score, GETDATE())";

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

        private void GoToRecordWindow()
        {
            if (_recordOpened) 
            {
                return;
            }
            _recordOpened = true;

            DisposeAllTimers();
            _recognizer?.CloseKinect();

            Onemind_record record = new Onemind_record();
            record.Show();
            this.Close();
        }


        // 타이머 정리
        private void DisposeAllTimers()
        {
            _logicTimer?.Stop();
            _logicTimer?.Dispose();

            _detectTimer?.Stop();

            foreach (var t in _tempTimers)
            {
                try { t.Stop(); } catch { }
            }
            _tempTimers.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            DisposeAllTimers();
            if (_recognizer != null)
            {
                _recognizer.ColorHalvesUpdated -= Recognizer_ColorHalvesUpdated;
                try { _recognizer.CloseKinect(); } catch { }
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in _tempTimers) // 모든 임시 타이머 정지
            {
                try { t.Stop(); }
                catch { }
            }

            _gameRunning = false; // 게임 종료 상태로 변경
            _currentQuestionText = null; // 현재 문제 초기화

            SaveScoreToDB(); // 점수 DB 저장    
            GoToRecordWindow(); // 기록 창으로 이동
        }
    }
}
