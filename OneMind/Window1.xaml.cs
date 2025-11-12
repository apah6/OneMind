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
    /// <summary>
    /// Window1.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Window1 : Window
    {
        private KinectSensor _kinectSensor1; // 첫 번째 키넥트 센서
        private KinectSensor _kinectSensor2; // 두 번째 키넥트 센서


        private DispatcherTimer _timer; // 타이머
        private int _timeLeft = 3; // 3초 제한
        private bool _gameRunning = false; // 게임 상태 (초기 : 실행이 아닌 상태)

        public Window1()
        {
            InitializeComponent();
            InitializeKinect(); // 키넥트 초기화
            InitializeTimer(); // 타이머 초기화
        }

        private void InitializeKinect()
        {
            try
            {

                var sensors = KinectSensor.KinectSensors; // 연결된 Kinect 센서 목록 가져온다.

                if (sensors.Count > 0) // 하나 이상의 Kinect가 연결된 경우
                {
                    // 첫 번째 Kinect 연결
                    _kinectSensor1 = sensors[0]; // 첫 번째 Kinect 선택
                    _kinectSensor1.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30); // 컬러 스트림 활성화
                    _kinectSensor1.ColorFrameReady += Kinect1_ColorFrameReady; // 컬러 프레임 이벤트 핸들러 등록

                    _kinectSensor1.Start(); // 첫 번째 Kinect 시작
                   // _kinectSensor1 = sensors[0]; // 재 할당
                    lblPerceive1.Content = "1번 키넥트 연결됨";


                    // 두 번째 Kinect 연결
                    if (sensors.Count > 1)
                    {
                        _kinectSensor2 = sensors[1]; // 두 번째 Kinect 선택
                        _kinectSensor2.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30); // 컬러 스트림 활성화
                        _kinectSensor2.ColorFrameReady += Kinect2_ColorFrameReady; // 컬러 프레임 이벤트 핸들러 등록

                        _kinectSensor2.Start(); // 두 번째 Kinect 시작
                     //  _kinectSensor2 = sensors[1]; // 재 할당
                        lblPerceive2.Content = "2번 키넥트 연결됨";

                    }
                    else
                    {
                        lblPerceive2.Content = "2번 키넥트 없음 ";

                    }
                }
                else
                {
                    lblPerceive1.Content = "키넥트 미연결 ";
                    lblPerceive2.Content = "키넥트 미연결 ";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("키넥트 초기화 오류입니다.: " + ex.Message);
            }
        }

        // 첫 번째 Kinect 영상 표시
        private void Kinect1_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {

            using (var frame = e.OpenColorImageFrame())
            {
                if (frame == null) return;

                byte[] pixels = new byte[frame.PixelDataLength];
                frame.CopyPixelDataTo(pixels);

                Dispatcher.Invoke(() =>
                {
                    imgPlayer1.Source = BitmapSource.Create(
                        frame.Width, frame.Height,
                        96, 96, PixelFormats.Bgr32,
                        null, pixels, frame.Width * frame.BytesPerPixel);
                });
            }
        }

        // 두 번째 Kinect 영상 표시
        private void Kinect2_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (var frame = e.OpenColorImageFrame())
            {
                if (frame == null) return;

                byte[] pixels = new byte[frame.PixelDataLength];
                frame.CopyPixelDataTo(pixels);

                Dispatcher.Invoke(() =>
                {
                    imgPlayer2.Source = BitmapSource.Create(
                        frame.Width, frame.Height,
                        96, 96, PixelFormats.Bgr32,
                        null, pixels, frame.Width * frame.BytesPerPixel);
                });
            }
        }

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1); // 1초 간격
            _timer.Tick += Timer_Tick; // Timer_Tick 이벤트 핸들러 등록


        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_gameRunning) // 게임이 실행 중이 아니라면
            {
                return;
            }

            _timeLeft--; // 1초 감소
            pgrTime.Value = _timeLeft; // 프로그레스 바 업데이트

            if (_timeLeft <= 0) // 시간이 다 돼면
            {
                _timer.Stop(); // 타이머 중지
                _gameRunning = false; //  게임 상태 변경  
                lblKeyword.Content = "시간 초과!";

            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e) // 일시 중단
        {
            _gameRunning = false; // 게임 상태 변경
            _timer.Stop(); // 타이머 중지
            _kinectSensor1?.Stop(); // 첫 번째 키넥트 중지
            _kinectSensor2?.Stop(); // 두 번째 키넥트 중지

            // 문제 푼데까지 저장하고 랭킹 회면으로 넘어감
            MessageBox.Show("게임이 종료되었습니다.");

        }
    }


}

