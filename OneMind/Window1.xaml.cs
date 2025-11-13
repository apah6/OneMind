using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;
using System.Windows.Threading;

namespace OneMind
{
    public partial class Window1 : Window
    {
        private KinectSensor _kinect; // Kinect 센서
        private byte[] _colorPixels; // 컬러 데이터
        private short[] _depthPixels; // 깊이 데이터

        private WriteableBitmap _bitmap1; // 사람1 비트맵
        private WriteableBitmap _bitmap2; // 사람2 비트맵

        private DispatcherTimer _timer;
        private int _timeLeft = 3;
        private bool _gameRunning = false;
        private bool _timerStarted = false;

        public Window1()
        {
            InitializeComponent();
            InitializeKinect();
            InitializeTimer();
        }

        private void InitializeKinect()
        {
            if (KinectSensor.KinectSensors.Count == 0)
            {
                MessageBox.Show("Kinect 연결을 확인하세요.");
                return;
            }

            _kinect = KinectSensor.KinectSensors[0];

            _kinect.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            _kinect.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            _kinect.SkeletonStream.Enable();

            _colorPixels = new byte[_kinect.ColorStream.FramePixelDataLength];
            _depthPixels = new short[_kinect.DepthStream.FramePixelDataLength];

            _bitmap1 = new WriteableBitmap(_kinect.ColorStream.FrameWidth, _kinect.ColorStream.FrameHeight, 96, 96, PixelFormats.Bgra32, null);
            _bitmap2 = new WriteableBitmap(_kinect.ColorStream.FrameWidth, _kinect.ColorStream.FrameHeight, 96, 96, PixelFormats.Bgra32, null);

            imgPlayer1.Source = _bitmap1;
            imgPlayer2.Source = _bitmap2;

            _kinect.AllFramesReady += Kinect_AllFramesReady;
            _kinect.Start();
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

        private void Kinect_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (colorFrame == null || skeletonFrame == null) return;

                colorFrame.CopyPixelDataTo(_colorPixels);

                Skeleton[] skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                skeletonFrame.CopySkeletonDataTo(skeletons);

                byte[] pixels1 = new byte[_colorPixels.Length];
                byte[] pixels2 = new byte[_colorPixels.Length];

                bool player1Detected = false;
                bool player2Detected = false;

                int trackedCount = 0;

                foreach (Skeleton skeleton in skeletons)
                {
                    if (skeleton.TrackingState != SkeletonTrackingState.Tracked) continue;

                    SkeletonPoint head = skeleton.Joints[JointType.Head].Position;
                    SkeletonPoint hip = skeleton.Joints[JointType.HipCenter].Position;

                    DepthImagePoint headDepth = _kinect.MapSkeletonPointToDepth(head, DepthImageFormat.Resolution640x480Fps30);
                    DepthImagePoint hipDepth = _kinect.MapSkeletonPointToDepth(hip, DepthImageFormat.Resolution640x480Fps30);

                    int minX = Math.Max(0, Math.Min(headDepth.X, hipDepth.X));
                    int maxX = Math.Min(_bitmap1.PixelWidth - 1, Math.Max(headDepth.X, hipDepth.X));
                    int minY = Math.Max(0, Math.Min(headDepth.Y, hipDepth.Y));
                    int maxY = Math.Min(_bitmap1.PixelHeight - 1, Math.Max(headDepth.Y, hipDepth.Y));

                    byte[] targetPixels;

                    if (trackedCount == 0)
                    {
                        targetPixels = pixels1;
                        player1Detected = true;
                    }
                    else if (trackedCount == 1)
                    {
                        targetPixels = pixels2;
                        player2Detected = true;
                    }
                    else
                    {
                        break; // 2명 이상이면 무시
                    }

                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int x = minX; x <= maxX; x++)
                        {
                            int idx = (y * _bitmap1.PixelWidth + x) * 4;
                            Buffer.BlockCopy(_colorPixels, idx, targetPixels, idx, 4);
                        }
                    }

                    trackedCount++;
                }

                // 만약 플레이어가 1명뿐이면 Player2 화면은 검은색으로 초기화
                if (!player2Detected)
                    Array.Clear(pixels2, 0, pixels2.Length);

                // 비트맵 업데이트
                _bitmap1.WritePixels(new Int32Rect(0, 0, _bitmap1.PixelWidth, _bitmap1.PixelHeight), pixels1, _bitmap1.PixelWidth * 4, 0);
                _bitmap2.WritePixels(new Int32Rect(0, 0, _bitmap2.PixelWidth, _bitmap2.PixelHeight), pixels2, _bitmap2.PixelWidth * 4, 0);

                // 인식 상태 표시
                lblPerceive1.Content = player1Detected ? "Player1 인식됨" : "대기 중...";
                lblPerceive2.Content = player2Detected ? "Player2 인식됨" : "대기 중...";

                // 타이머 시작
                if (!_timerStarted && (player1Detected || player2Detected))
                {
                    _timerStarted = true;
                    StartGame(false);
                }
            }
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
            if (_kinect != null)
            {
                _kinect.AllFramesReady -= Kinect_AllFramesReady;
                if (_kinect.IsRunning) _kinect.Stop();
                _kinect = null;
            }

            if (_timer != null && _timer.IsEnabled)
                _timer.Stop();
        }
    }
}
