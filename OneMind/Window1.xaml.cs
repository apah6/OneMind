using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Kinect;

namespace OneMind
{
    public partial class Window1 : Window
    {
        private DispatcherTimer _timer;
        private int _timeLeft = 3;
        private bool _gameRunning = false;
        private bool _timerStarted = false;

        private Recognize _recognizer;

        // Player 2용 Skeleton 마스킹 비트맵
        private WriteableBitmap _player2Bitmap;

        public Window1(Recognize recognizer)
        {
            InitializeComponent();

            _recognizer = recognizer;

            if (_recognizer != null)
            {
                // Player 1: 컬러 영상 그대로
                imgPlayer1.Source = _recognizer.ColorBitmap;

                // Player 2: Skeleton 영역만 표시할 WriteableBitmap 초기화
                _player2Bitmap = new WriteableBitmap(
                    _recognizer.ColorBitmap.PixelWidth,
                    _recognizer.ColorBitmap.PixelHeight,
                    96, 96, PixelFormats.Bgr32, null);
                imgPlayer2.Source = _player2Bitmap;

                // Skeleton 이벤트 구독
                _recognizer.Sensor.SkeletonFrameReady += Sensor_SkeletonFrameReady;
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

        private void Sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame frame = e.OpenSkeletonFrame())
            {
                if (frame == null) return;

                Skeleton[] skeletons = new Skeleton[frame.SkeletonArrayLength];
                frame.CopySkeletonDataTo(skeletons);

                bool player1Detected = false;
                bool player2Detected = false;
                int trackedCount = 0;

                foreach (Skeleton skeleton in skeletons)
                {
                    if (skeleton.TrackingState != SkeletonTrackingState.Tracked) continue;

                    if (trackedCount == 0) player1Detected = true;
                    else if (trackedCount == 1) player2Detected = true;
                    else break;

                    trackedCount++;
                }

                // UI 업데이트
                lblPerceive1.Dispatcher.Invoke(() =>
                    lblPerceive1.Content = player1Detected ? "Player1 인식됨" : "대기 중...");
                lblPerceive2.Dispatcher.Invoke(() =>
                    lblPerceive2.Content = player2Detected ? "Player2 인식됨" : "대기 중...");

                // Player 2 Skeleton 영역 마스킹
                UpdatePlayer2Bitmap(skeletons);

                // 두 명 감지 시 타이머 시작
                if (!_timerStarted && player1Detected && player2Detected)
                {
                    _timerStarted = true;
                    StartGame(false);
                }
            }
        }

        private void UpdatePlayer2Bitmap(Skeleton[] skeletons)
        {
            if (_recognizer.ColorPixels == null) return;

            int width = _recognizer.ColorBitmap.PixelWidth;
            int height = _recognizer.ColorBitmap.PixelHeight;

            // 원본 컬러 복사
            byte[] pixels = new byte[_recognizer.ColorPixels.Length];
            Array.Copy(_recognizer.ColorPixels, pixels, pixels.Length);

            foreach (Skeleton skeleton in skeletons)
            {
                if (skeleton.TrackingState != SkeletonTrackingState.Tracked) continue;

                foreach (Joint joint in skeleton.Joints.Values)
                {
                    // Skeleton 좌표 → Color 이미지 좌표 변환
                    ColorImagePoint point = _recognizer.Sensor.MapSkeletonPointToColor(joint.Position, ColorImageFormat.RgbResolution640x480Fps30);
                    int x = point.X;
                    int y = point.Y;

                    if (x < 0 || x >= width || y < 0 || y >= height) continue;

                    int index = (y * width + x) * 4;

                    // Skeleton 영역만 표시하고 나머지는 검정으로
                    pixels[index] = _recognizer.ColorPixels[index];       // B
                    pixels[index + 1] = _recognizer.ColorPixels[index + 1]; // G
                    pixels[index + 2] = _recognizer.ColorPixels[index + 2]; // R
                    pixels[index + 3] = 255; // A
                }
            }

            // WriteableBitmap 업데이트
            _player2Bitmap.Dispatcher.Invoke(() =>
            {
                _player2Bitmap.WritePixels(
                    new Int32Rect(0, 0, width, height),
                    pixels,
                    width * 4,
                    0);
            });
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_timer != null && _timer.IsEnabled)
                _timer.Stop();

            _gameRunning = false;
            _timerStarted = false;
            lblKeyword.Content = "게임이 중단되었습니다.";
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_recognizer?.Sensor != null)
            {
                _recognizer.Sensor.SkeletonFrameReady -= Sensor_SkeletonFrameReady;
            }

            if (_timer != null && _timer.IsEnabled)
                _timer.Stop();
        }
    }
}
