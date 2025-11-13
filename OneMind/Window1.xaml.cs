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
using Microsoft.Kinect;
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
                MessageBox.Show("Kinect 연결을 확인하세요.");
               // this.Close(); // 창 닫기 (Kinect 없으면 게임 불가) 일단 주석
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

        private void StartGame(Boolean isResume = false)
        {
            _gameRunning = true; // 게임 진행 상태 설정

            if (!isResume) // 새 게임일 때만 시간 초기화
            {
                _timeLeft = 3; // 남은 시간 초기화
                pgrTime.Value = 0; // 프로그레스바 초기화
                lblKeyword.Content = "게임 시작!";
            }
            else // 게임 재개 시
            {
                lblKeyword.Content = $"게임 재개! 남은 시간: {_timeLeft}초"; // 남은 시간 표시
                pgrTime.Value = 3 - _timeLeft; // 프로그레스바 업데이트
            }

            pgrTime.Maximum = 3; // 프로그레스바 최대값 설정
            _timer.Start(); // 타이머 시작
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_gameRunning) // 게임이 진행 중이 아니면
            {
                return;
            }

            //  게임이 진행 중이면 시간 감소
            _timeLeft--;
            pgrTime.Value = 3 - _timeLeft;
            lblKeyword.Content = $"남은 시간: {_timeLeft}초";

            if (_timeLeft <= 0) // 시간이 다 되었으면
            {
                _timer.Stop(); // 타이머 중지
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
                if (player1Detected && player2Detected) // 두 사람 모두 인식되면
                {
                    if (!_timerStarted) // 타이머가 시작되지 않았으면
                    {
                        _timerStarted = true; // 타이머 시작 플래그 설정

                        if (!_gameRunning) // 게임이 일시정지 상태면 재개
                        {
                            StartGame(isResume: true);
                        }
                        else // 게임이 시작 전이면 새 게임
                        {
                            StartGame(isResume: false);
                        }
                    }
                }
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_timer != null && _timer.IsEnabled)
            {
                _timer.Stop(); // 타이머 중지
            }
            _gameRunning = false; // 게임 종료
            _timerStarted = false; // 타이머 시작 플래그 초기화
            lblKeyword.Content = "게임이 중단되었습니다.";
        }

        protected override void OnClosed(EventArgs e)
        {

            if (_kinect != null) // Kinect 센서가 존재하면
            {
                _kinect.AllFramesReady -= Kinect_AllFramesReady; // 이벤트 핸들러 해제
                if (_kinect.IsRunning) _kinect.Stop(); // Kinect 센서 중지
                _kinect = null; // Kinect 객체 해제
            }

            if (_timer != null && _timer.IsEnabled)
            {
                _timer.Stop(); // 타이머 중지    
            }
        }
    }
}
