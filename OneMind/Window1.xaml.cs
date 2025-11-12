using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OneMind
{
    public partial class Window1 : Window
    {
        private KinectSensor _kinect; // Kinect 센서 객체
        private byte[] _colorPixels; // 컬러 프레임 데이터
        private short[] _depthPixels; // 깊이 프레임 데이터

        private WriteableBitmap _bitmap1; // 사람 1용 비트맵
        private WriteableBitmap _bitmap2; // 사람 2용 비트맵

        private DispatcherTimer _timer; // 게임 타이머
        private int _timeLeft = 3; // 3초 타이머

        private bool _gameRunning = false; // 게임 진행 상태 (초기값: false)
        private bool _timerStarted = false; // 타이머 시작 여부 (초기값: false)

        public Window1()
        {
            InitializeComponent(); 
            InitializeKinect(); // Kinect 초기화
            InitializeTimer(); // 타이머 초기화
        }

        private void InitializeKinect()
        {
            if (KinectSensor.KinectSensors.Count == 0) // Kinect 센서가 없는 경우
            {
                MessageBox.Show("Kinect 센서를 찾을 수 없습니다.");
                // Close(); // 창 닫기
                return;
            }

            _kinect = KinectSensor.KinectSensors[0];

            _kinect.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30); // 컬러 스트림 활성화
            _kinect.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30); // 깊이 스트림 활성화
            _kinect.SkeletonStream.Enable();

            _colorPixels = new byte[_kinect.ColorStream.FramePixelDataLength]; // 컬러 데이터 배열 초기화
            _depthPixels = new short[_kinect.DepthStream.FramePixelDataLength]; // 깊이 데이터 배열 초기화

            _bitmap1 = new WriteableBitmap(_kinect.ColorStream.FrameWidth, _kinect.ColorStream.FrameHeight, 96, 96, PixelFormats.Bgra32, null); // 사람 1용 비트맵 초기화
            _bitmap2 = new WriteableBitmap(_kinect.ColorStream.FrameWidth, _kinect.ColorStream.FrameHeight, 96, 96, PixelFormats.Bgra32, null); // 사람 2용 비트맵 초기화
             
            imgPlayer1.Source = _bitmap1; // 이미지 컨트롤에 비트맵 설정 
            imgPlayer2.Source = _bitmap2; // 이미지 컨트롤에 비트맵 설정

            _kinect.AllFramesReady += Kinect_AllFramesReady; // 프레임 준비 이벤트 핸들러 등록
            _kinect.Start(); // Kinect 센서 시작
        }

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer(); // 디스패처 타이머 초기화
            _timer.Interval = TimeSpan.FromSeconds(1); // 1초 간격
            _timer.Tick += Timer_Tick; // Timer_Tick 이벤트 핸들러 등록
        }

        private void StartGame()
        {
            _gameRunning = true; // 게임 진행 시작
            _timeLeft = 3; // 타이머 초기화
            pgrTime.Maximum = 3; // 프로그레스바 최대값 설정 (3초)
            pgrTime.Value = 0; // 프로그레스바 초기값 설정
            lblKeyword.Content = "게임 시작!";  // 게임 시작 메시지 표시
            _timer.Start();    // 타이머 시작
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_gameRunning) // 게임이 진행 중이 아니면 반환
            {
                return;
            }

            _timeLeft--; // 남은 시간 1씩 감소
            pgrTime.Value = 3 - _timeLeft; // 프로그레스바 업데이트
            lblKeyword.Content = $"남은 시간: {_timeLeft}초"; // 남은 시간 표시

            if (_timeLeft <= 0) // 시간이 다 되었으면
            {
                _timer.Stop(); //   타이머 중지
                lblKeyword.Content = "시간 종료!";
                _gameRunning = false; // 게임 종료
                _timerStarted = false; // 타이머 시작 플래그 초기화
            }
        }

        private void Kinect_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame()) // 컬러 프레임 열기
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame()) // 깊이 프레임 열기
            {
                if (colorFrame == null || depthFrame == null) // 컬러 프레임이, 깊이 프레임이 유효하지 않으면 반환
                {
                    return;
                }

                colorFrame.CopyPixelDataTo(_colorPixels); // 컬러 데이터 복사
                depthFrame.CopyPixelDataTo(_depthPixels); // 깊이 데이터 복사

                byte[] player1Pixels = new byte[_colorPixels.Length]; // 사람 1용 픽셀 배열
                byte[] player2Pixels = new byte[_colorPixels.Length]; // 사람 2용 픽셀 배열
                bool player1Detected = false, player2Detected = false; // 사람 인식 여부 플래그

                for (int i = 0; i < _depthPixels.Length; i++) // 모든 픽셀에 대해 반복
                {
                    int playerIndex = _depthPixels[i] & DepthImageFrame.PlayerIndexBitmask; // 플레이어 인덱스 추출

                    if (playerIndex == 1) // 사람 1인 경우
                    {
                        Buffer.BlockCopy(_colorPixels, i * 4, player1Pixels, i * 4, 4); // 사람 1 픽셀 복사
                        player1Detected = true; // 사람 1 인식 플래그 설정
                    }
                    else if (playerIndex == 2) // 사람 2인 경우
                    {
                        Buffer.BlockCopy(_colorPixels, i * 4, player2Pixels, i * 4, 4); // 사람 2 픽셀 복사
                        player2Detected = true; // 사람 2 인식 플래그 설정
                    }
                }

                _bitmap1.WritePixels(new Int32Rect(0, 0, _bitmap1.PixelWidth, _bitmap1.PixelHeight), player1Pixels, _bitmap1.PixelWidth * 4, 0); // 사람 1 비트맵 업데이트
                _bitmap2.WritePixels(new Int32Rect(0, 0, _bitmap2.PixelWidth, _bitmap2.PixelHeight), player2Pixels, _bitmap2.PixelWidth * 4, 0); // 사람 2 비트맵 업데이트

                lblPerceive1.Content = player1Detected ? "Player1 인식됨" : "대기 중..."; // 사람 1 인식 상태 업데이트
                lblPerceive2.Content = player2Detected ? "Player2 인식됨" : "대기 중..."; // 사람 2 인식 상태 업데이트

                // 두 사람 모두 인식되면 게임 시작
                if (!_timerStarted && player1Detected && player2Detected)
                {
                    _timerStarted = true; // 타이머 시작 플래그 설정
                    StartGame(); // 게임 시작
                }
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop(); // 타이머 중지
            _gameRunning = false; // 게임 종료
            _timerStarted = false; // 타이머 시작 플래그 초기화
            lblKeyword.Content = "게임이 중단되었습니다.";
        }

        protected override void OnClosed(EventArgs e)
        {
            //base.OnClosed(e); // 기본 닫기 동작 호출

            if (_kinect != null) // Kinect 센서가 존재하면
            {
                _kinect.AllFramesReady -= Kinect_AllFramesReady; // 이벤트 핸들러 해제
                if (_kinect.IsRunning) _kinect.Stop(); // Kinect 센서 중지
                _kinect = null; // Kinect 객체 해제
            }

            _timer?.Stop(); // 타이머 중지
        }
    }
}
