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
        private DispatcherTimer _detectTimer;
        private List<DispatcherTimer> _tempTimers = new List<DispatcherTimer>();

        private int _timeLeftTicks = 30;
        private const int MaxTicks = 30;

        private bool _gameRunning = false;
        private bool _gameInitialized = false;
        private int _currentQuestion = 0;
        private int _maxQuestions = 10;
        private bool _lastCorrect = false;
        private int _score = 0;

        private string TeamName;
        private int Category_ID;
        private string _connStr = @"Server=localhost\SQLEXPRESS;Database=TestDB;Trusted_Connection=True;";
        private bool _recordOpened = false;
        private Recognize _recognizer;

        private List<int> _usedQuestionIds = new List<int>();
        private int _currentQuestionId;
        private string _currentQuestionText;

        private WriteableBitmap _leftBitmap;
        private WriteableBitmap _rightBitmap;
        private byte[] _leftPixels;
        private byte[] _rightPixels;

        public Window1(Recognize recognizer, string teamName, int categoryName)
        {
            InitializeComponent();
            _recognizer = recognizer;
            TeamName = teamName;
            Category_ID = categoryName;

            if (_recognizer != null)
                _recognizer.ColorHalvesUpdated += Recognizer_ColorHalvesUpdated;

            InitDetectionTimer();
        }

        private void Recognizer_ColorHalvesUpdated(WriteableBitmap leftFrame, WriteableBitmap rightFrame)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                InitBitmapsIfNull(leftFrame, rightFrame);
                UpdateBitmap(_leftBitmap, _leftPixels, leftFrame);
                UpdateBitmap(_rightBitmap, _rightPixels, rightFrame);
            }));
        }

        private void InitBitmapsIfNull(WriteableBitmap leftFrame, WriteableBitmap rightFrame)
        {
            if (_leftBitmap != null && _rightBitmap != null) return;

            _leftBitmap = new WriteableBitmap(leftFrame.PixelWidth, leftFrame.PixelHeight, leftFrame.DpiX, leftFrame.DpiY, leftFrame.Format, null);
            _rightBitmap = new WriteableBitmap(rightFrame.PixelWidth, rightFrame.PixelHeight, rightFrame.DpiX, rightFrame.DpiY, rightFrame.Format, null);
            imgPlayer1.Source = _leftBitmap;
            imgPlayer2.Source = _rightBitmap;

            _leftPixels = new byte[leftFrame.PixelHeight * leftFrame.PixelWidth * (leftFrame.Format.BitsPerPixel / 8)];
            _rightPixels = new byte[rightFrame.PixelHeight * rightFrame.PixelWidth * (rightFrame.Format.BitsPerPixel / 8)];
        }

        private void UpdateBitmap(WriteableBitmap bitmap, byte[] pixels, WriteableBitmap frame)
        {
            int stride = frame.PixelWidth * (frame.Format.BitsPerPixel / 8);
            frame.CopyPixels(pixels, stride, 0);

            bitmap.Lock();
            bitmap.WritePixels(new Int32Rect(0, 0, frame.PixelWidth, frame.PixelHeight), pixels, stride, 0);
            bitmap.AddDirtyRect(new Int32Rect(0, 0, frame.PixelWidth, frame.PixelHeight));
            bitmap.Unlock();
        }
     

        
        private void InitDetectionTimer()
        {
            _detectTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _detectTimer.Tick += CheckPlayersDetected;
            _detectTimer.Start();
        }

        private void CheckPlayersDetected(object sender, EventArgs e)
        {
            if (_recognizer == null) return;

            UpdatePlayerStatus();

            if (!_gameInitialized && BothPlayersDetected())
            {
                _gameInitialized = true;
                StartGame();
                LoadNextQuestion();
            }
            else
            {
                ResumeTimerIfPlayersDetected();
            }
        }

        private void UpdatePlayerStatus()
        {
            lblPerceive1.Content = _recognizer.IsPlayer1Detected() ? "Player1 인식됨" : "대기 중...";
            lblPerceive2.Content = _recognizer.IsPlayer2Detected() ? "Player2 인식됨" : "대기 중...";
        }

        private bool BothPlayersDetected() => _recognizer.IsPlayer1Detected() && _recognizer.IsPlayer2Detected();

        private void ResumeTimerIfPlayersDetected()
        {
            if (!_gameRunning || _timeLeftTicks <= 0) return;

            if (BothPlayersDetected())
            {
                lblKeyword.Content = _currentQuestionText ?? "게임 재개!";
                _logicTimer?.Start();
            }
            else
            {
                _logicTimer?.Stop();
                lblKeyword.Content = "플레이어 대기 중...";
            }
        }
    

        private void StartGame()
        {
            _gameRunning = true;
            _timeLeftTicks = MaxTicks;
            _score = 0;
            _currentQuestion = 0;
            lblScore.Content = $"{_score} / {_maxQuestions}";
            lblKeyword.Content = "게임 시작!";
        }

        private void StartLogicTimer()
        {
            _logicTimer?.Stop();
            _logicTimer?.Dispose();

            _logicTimer = new Timer(100);
            _logicTimer.Elapsed += (s, e) =>
            {
                if (!BothPlayersDetected()) return;

                _timeLeftTicks--;
                try { _lastCorrect = _recognizer.ComparePlayers(); } catch { }

                if (_timeLeftTicks <= 0)
                {
                    _logicTimer.Stop();
                    if (_lastCorrect) _score++;
                    Dispatcher.Invoke(FinishQuestion);
                }
            };
            _logicTimer.Start();
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
                            ResetProgressBar();
                            StartLogicTimer();
                        }
                        else
                        {
                            lblKeyword.Content = "문제를 다 풀었습니다.";
                            DelayAction(2, EndGame);
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
            _gameRunning = false;

            if (_currentQuestionId != 0 && !_usedQuestionIds.Contains(_currentQuestionId))
                _usedQuestionIds.Add(_currentQuestionId);

            _currentQuestion++;
            lblScore.Content = $"{_score} / {_maxQuestions}";
            lblKeyword.Content = _lastCorrect ? "정답입니다! (+1점)" : "오답입니다! (+0점)";

            DelayAction(1, LoadNextQuestion);
        }

        private void ResetProgressBar()
        {
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
        }

        private void DelayAction(double seconds, Action action)
        {
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                action();
                _tempTimers.Remove(timer);
            };
            _tempTimers.Add(timer);
            timer.Start();
        }
        
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
            if (_recordOpened) return;
            _recordOpened = true;

            DisposeAllTimers();
            _recognizer?.CloseKinect();

            Onemind_record record = new Onemind_record();
            record.Show();
            this.Close();
        }
     


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
            DisposeAllTimers();
            _gameRunning = false;
            _currentQuestionText = null;

            SaveScoreToDB();
            GoToRecordWindow();
        }
    }
}
