using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OneMind
{
    public partial class Window1 : Window
    {
        private DispatcherTimer _timer;
        private DispatcherTimer _detectTimer; // 플레이어 감지용 타이머
        private List<DispatcherTimer> _tempTimers = new List<DispatcherTimer>();

        // 시간을 3초가 아니라 0.1초 단위의 '틱'으로 관리 (3초 = 30틱)
        private int _timeLeftTicks = 30;
        private const int MaxTicks = 30; // 3초 기준

        private bool _gameRunning = false;
        private bool _gameInitialized = false;
        private int _currentQuestion = 0; // 현재 문제 번호
        private int _maxQuestions = 10; // 최대 문제 수
        private bool _lastCorrect = false; // 문제 정답 여부
        private int _score = 0; // 점수
        private string TeamName; // 팀명
        private int Category_ID; // 카테고리명
        private string _connStr = @"Server=localhost\SQLEXPRESS;Database=TestDB;Trusted_Connection=True;";
        private bool _recordOpened = false;
        private Recognize _recognizer;

        // 이미 출제된 제시어 관리 (종료 후 재시작하면 다시 나오게)
        private List<int> _usedQuestionIds = new List<int>();
        private int _currentQuestionId; // 현재 문제의 ID 저장 (중복 출제 방지)
        private string _currentQuestionText; // 현재 문제의 텍스트 저장

        // WriteableBitmap 미리 생성
        private WriteableBitmap _leftBitmap;
        private WriteableBitmap _rightBitmap;

        // 최적화: 픽셀 배열 재사용
        private byte[] _leftPixels;
        private byte[] _rightPixels;

        public Window1(Recognize recognizer, string teamName, int categoryName)
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

        // 게임 타이머 정리 함수
        private void DisposeGameTimer()
        {
            if (_timer != null)
            {
                try
                {
                    _timer.Stop();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("타이머 정지 오류: " + ex.Message);
                }

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
                catch (Exception ex)
                {
                    MessageBox.Show("감지 타이머 정지 오류: " + ex.Message);
                }

                _detectTimer.Tick -= CheckPlayersDetected;
                _detectTimer = null;
            }
        }

        // Kinect 영상 갱신 이벤트 (최적화 적용)
        private void Recognizer_ColorHalvesUpdated(System.Windows.Media.Imaging.WriteableBitmap leftFrame, System.Windows.Media.Imaging.WriteableBitmap rightFrame)
        {
            // 이벤트가 UI 스레드에서 호출되지 않을 수 있으므로 안전하게 Dispatcher 사용
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                // 최초 한 번만 WriteableBitmap 생성
                if (_leftBitmap == null || _rightBitmap == null)
                {
                    _leftBitmap = new WriteableBitmap(leftFrame.PixelWidth, leftFrame.PixelHeight, leftFrame.DpiX, leftFrame.DpiY, leftFrame.Format, null);
                    _rightBitmap = new WriteableBitmap(rightFrame.PixelWidth, rightFrame.PixelHeight, rightFrame.DpiX, rightFrame.DpiY, rightFrame.Format, null);
                    imgPlayer1.Source = _leftBitmap;
                    imgPlayer2.Source = _rightBitmap;

                    // 픽셀 배열 미리 생성 (GC 최소화)
                    _leftPixels = new byte[leftFrame.PixelHeight * leftFrame.PixelWidth * (leftFrame.Format.BitsPerPixel / 8)];
                    _rightPixels = new byte[rightFrame.PixelHeight * rightFrame.PixelWidth * (rightFrame.Format.BitsPerPixel / 8)];
                }

                // 픽셀 데이터 덮어쓰기 (새 배열 생성 X)
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
                catch (Exception ex)
                {
                    MessageBox.Show("Kinect 종료 오류: " + ex.Message);
                }
            }

            foreach (var t in _tempTimers)
            {
                try { t.Stop(); }
                catch { }
            }
            _tempTimers.Clear();
        }

        // 플레이어 인식 확인
        private void CheckPlayersDetected(object sender, EventArgs e)
        {
            if (_recognizer == null)
            {
                return;
            }

            bool player1 = _recognizer.IsPlayer1Detected();
            bool player2 = _recognizer.IsPlayer2Detected();

            lblPerceive1.Content = player1 ? "Player1 인식됨" : "대기 중...";
            lblPerceive2.Content = player2 ? "Player2 인식됨" : "대기 중...";

            // 두 명 모두 인식되면 게임 시작
            if (!_gameInitialized && player1 && player2)
            {
                _gameInitialized = true;
                StartGame();
                LoadNextQuestion(); // 첫 문제 로드 및 타이머 시작
            }
            else
            {
                // 이미 게임 중이라면 재감지 시 타이머 재개함
                ResumeTimerIfPlayersDetected();
            }
        }

        private void ResumeTimerIfPlayersDetected()
        {
            // 두 플레이어가 감지되고, 타이머가 멈춰있으며 현재 문제 진행 중이고 남은 시간이 있을 때 타이머 재개
            if (_timer != null && _recognizer.IsPlayer1Detected() && _recognizer.IsPlayer2Detected() && !_timer.IsEnabled && _gameRunning && _timeLeftTicks > 0)
            {
                lblKeyword.Content = "게임 재개!";
                DispatcherTimer resumeDelayTimer = new DispatcherTimer();
                resumeDelayTimer.Interval = TimeSpan.FromSeconds(1);
                resumeDelayTimer.Tick += (s, e) =>
                {
                    resumeDelayTimer.Stop();
                    // 제시어가 유효할 경우에만 복구
                    if (!string.IsNullOrEmpty(_currentQuestionText))
                    {
                        lblKeyword.Content = _currentQuestionText;
                    }
                    _timer.Start(); // 제시어 복구 후 타이머 재개  
                    _tempTimers.Remove(resumeDelayTimer);
                };
                _tempTimers.Add(resumeDelayTimer);
                resumeDelayTimer.Start();
            }
        }

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += Timer_Tick;
        }

        private void StartGame()
        {
            _gameRunning = true;
            _timeLeftTicks = MaxTicks;
            pgrTime.Maximum = MaxTicks;
            pgrTime.Value = 0;

            lblKeyword.Content = "게임 시작!";
            _score = 0;
            _currentQuestion = 0;

            lblScore.Content = $"{_score} / {_maxQuestions}";
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_gameRunning)
                return;

            // 플레이어 감지 안 될 때 정지
            if (!_recognizer.IsPlayer1Detected() || !_recognizer.IsPlayer2Detected())
            {
                lblKeyword.Content = "플레이어 재인식 중...";
                if (_timer.IsEnabled) _timer.Stop();
                return;
            }

            // 시간 감소
            _timeLeftTicks--;
            pgrTime.Value = MaxTicks - _timeLeftTicks;

            // 정답인지 체크 → 즉시 반응하지 않고, flag만 저장
            try
            {
                _lastCorrect = _recognizer.ComparePlayers();
            }
            catch (Exception ex)
            {
                MessageBox.Show("정답 비교 오류: " + ex.Message);
            }

            // 시간 종료
            if (_timeLeftTicks <= 0)
            {
                _timer.Stop();

                // 5초가 끝난 시점에서 점수 반영
                if (_lastCorrect)
                    _score++;

                FinishQuestion();
            }
        }

        private void GoToRecordWindow()
        {
            // 중복 실행 방지
            if (_recordOpened)
            {
                return;
            }
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
                catch (Exception ex)
                {
                    MessageBox.Show("Kinect 종료 오류: " + ex.Message);
                }
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


            foreach (var t in _tempTimers)
            {
                try { t.Stop(); }
                catch { }
            }

            _gameRunning = false;
            _currentQuestionText = null;

            SaveScoreToDB(); // 점수 DB 저장    

            GoToRecordWindow(); // 기록 창으로 이동
        }

        private void FinishQuestion()
        {
            _gameRunning = false;

            if (_currentQuestionId != 0) // 유효한 문제 ID가 있을 때만 처리
            {
                if (!_usedQuestionIds.Contains(_currentQuestionId))
                {
                    _usedQuestionIds.Add(_currentQuestionId); // 출제된 문제 ID 추가
                }
                _currentQuestionId = 0; // 초기화
            }
            _currentQuestionText = null;
            // lblScore (점수)를 여기서 강제로 즉시 새로고침
            lblScore.Content = $"{_score} / {_maxQuestions}";
            lblScore.UpdateLayout();  // 즉시 갱신

            _currentQuestion++;

            if (_currentQuestion >= _maxQuestions)
            {
                EndGame();
                return;
            }

            lblKeyword.Content = _lastCorrect ? "정답입니다! (+1점)" : "오답입니다! (+0점)";

            // 1초 뒤에 다음 문제 로드
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

        private void EndGame()
        {
            _gameRunning = false;
            lblKeyword.Content = "게임 종료! 점수 기록 중...";

            SaveScoreToDB();
            GoToRecordWindow();
            _currentQuestionText = null;
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

                    // 이미 출제된 문제 ID들을 콤마로 연결 (예: "1,5,7")
                    // 리스트가 비어있으면 "0"을 넣어 에러 방지
                    string notInClause = _usedQuestionIds.Count > 0
                                         ? string.Join(",", _usedQuestionIds)
                                         : "0";
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
                            // 0번 컬럼 = Game_Word_ID (int)
                            int questionId = reader.GetInt32(0);

                            // 1번 컬럼 = Game_Word (string)
                            string questionText = reader.GetString(1);

                            _currentQuestionId = questionId;
                            _currentQuestionText = questionText; // 제시어 저장
                            lblKeyword.Content = questionText;

                            _timeLeftTicks = MaxTicks;
                            pgrTime.Value = 0;

                            _gameRunning = true;

                            if (_recognizer.IsPlayer1Detected() && _recognizer.IsPlayer2Detected())
                            {
                                _timer.Start();
                            }
                            else
                            {
                                _timer.Stop();
                                lblKeyword.Content = "플레이어 대기 중...";
                            }
                        }
                        else
                        {
                            lblKeyword.Content = "문제를 다 풀었습니다.";

                            DispatcherTimer finalDelayTimer = new DispatcherTimer();
                            finalDelayTimer.Interval = TimeSpan.FromSeconds(2); // 2초 지연 설정
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
    }
}
