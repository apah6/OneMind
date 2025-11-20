using Microsoft.Kinect;
using OneMind;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

public class Recognize
{
    // 키넥트
    private KinectSensor kinectsensor;

    // Color 스트림용
    private WriteableBitmap colorBitmap;
    private byte[] colorPixels;

    // Depth 스트림용
    private WriteableBitmap depthBitmap;
    private short[] depthPixels;
    private byte[] depthColorPixels;

    // Skeleton 스트림용
    public Skeleton[] Skeletons { get; private set; }

    public KinectSensor Sensor => kinectsensor;
    public WriteableBitmap ColorBitmap => colorBitmap;
    public WriteableBitmap DepthBitmap => depthBitmap;

    //플레이어 데이터
    public Player players;

    // players 접근 동기화용 락
    private readonly object _playersLock = new object();

    // 이벤트: 컬러 프레임을 좌/우로 나눈 이미지를 UI로 보냄
    public event Action<WriteableBitmap, WriteableBitmap> ColorHalvesUpdated;

    public Recognize()
    {
        players = new Player();
        InitializeKinect();
    }

    private void InitializeKinect()
    {
        try
        {
            // Kinect 연결 체크
            if (KinectSensor.KinectSensors.Count == 0)
            {
                MessageBox.Show("Kinect 센서를 찾을 수 없습니다.");
                return;
            }

            kinectsensor = KinectSensor.KinectSensors[0];

            // Color 스트림 활성화
            kinectsensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            colorPixels = new byte[kinectsensor.ColorStream.FramePixelDataLength];
            colorBitmap = new WriteableBitmap(
                kinectsensor.ColorStream.FrameWidth,
                kinectsensor.ColorStream.FrameHeight,
                96.0, 96.0,
                PixelFormats.Bgr32,
                null);
            kinectsensor.ColorFrameReady += KinectSensor_ColorFrameReady;

            // Depth 스트림 활성화
            kinectsensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
            depthPixels = new short[kinectsensor.DepthStream.FramePixelDataLength];
            depthColorPixels = new byte[kinectsensor.DepthStream.FramePixelDataLength * 4]; // BGR32
            depthBitmap = new WriteableBitmap(
                kinectsensor.DepthStream.FrameWidth,
                kinectsensor.DepthStream.FrameHeight,
                96.0, 96.0,
                PixelFormats.Bgr32,
                null);
            kinectsensor.DepthFrameReady += KinectSensor_DepthFrameReady;

            // Skeleton 스트림
            kinectsensor.SkeletonStream.Enable();
            kinectsensor.SkeletonFrameReady += KinectSensor_SkeletonFrameReady;

            Skeletons = new Skeleton[kinectsensor.SkeletonStream.FrameSkeletonArrayLength];

            // Kinect 시작
            kinectsensor.Start();
            MessageBox.Show("Kinect 초기화 완료!");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Kinect 초기화 중 오류: " + ex.Message);
        }
    }

    private void KinectSensor_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
    {
        using (ColorImageFrame frame = e.OpenColorImageFrame())
        {
            if (frame == null) return;

            // colorPixels에 최신 프레임 복사
            frame.CopyPixelDataTo(colorPixels);

            // UI 스레드 안전하게 전체 프레임 텍스처 업데이트
            Application.Current.Dispatcher.Invoke(() =>
            {
                colorBitmap.WritePixels(
                    new Int32Rect(0, 0, frame.Width, frame.Height),
                    colorPixels,
                    frame.Width * sizeof(int),
                    0);
            });

            // 좌/우 반으로 나눠서 이벤트로 전달
            int frameW = frame.Width;
            int frameH = frame.Height;
            int leftW = frameW / 2;
            int rightW = frameW - leftW;

            if (leftW <= 0 || rightW <= 0) return;

            int leftStride = leftW * 4;
            int rightStride = rightW * 4;
            byte[] leftPixels = new byte[leftW * frameH * 4];
            byte[] rightPixels = new byte[rightW * frameH * 4];

            // 행 단위로 복사
            for (int row = 0; row < frameH; row++)
            {
                int srcRowStart = row * frameW * 4;
                Buffer.BlockCopy(colorPixels, srcRowStart, leftPixels, row * leftStride, leftStride);
                Buffer.BlockCopy(colorPixels, srcRowStart + leftStride, rightPixels, row * rightStride, rightStride);
            }

            var leftBmp = new WriteableBitmap(leftW, frameH, 96.0, 96.0, PixelFormats.Bgr32, null);
            leftBmp.WritePixels(new Int32Rect(0, 0, leftW, frameH), leftPixels, leftStride, 0);

            var rightBmp = new WriteableBitmap(rightW, frameH, 96.0, 96.0, PixelFormats.Bgr32, null);
            rightBmp.WritePixels(new Int32Rect(0, 0, rightW, frameH), rightPixels, rightStride, 0);

            // UI 스레드에서 이벤트 발생 (비동기)
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ColorHalvesUpdated?.Invoke(leftBmp, rightBmp);
            }));
        }
    }

    private void KinectSensor_DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
    {
        using (DepthImageFrame frame = e.OpenDepthImageFrame())
        {
            if (frame == null) return;

            frame.CopyPixelDataTo(depthPixels);

            for (int i = 0; i < depthPixels.Length; i++)
            {
                short depth = depthPixels[i];
                byte intensity = (byte)(depth >= frame.MinDepth && depth <= frame.MaxDepth ? depth % 256 : 0);

                depthColorPixels[i * 4 + 0] = intensity; // B
                depthColorPixels[i * 4 + 1] = intensity; // G
                depthColorPixels[i * 4 + 2] = intensity; // R
                depthColorPixels[i * 4 + 3] = 255;       // A
            }

            // UI 스레드 안전하게 업데이트
            Application.Current.Dispatcher.Invoke(() =>
            {
                depthBitmap.WritePixels(
                    new Int32Rect(0, 0, frame.Width, frame.Height),
                    depthColorPixels,
                    frame.Width * 4,
                    0);
            });
        }
    }

    private void KinectSensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
    {
        using (SkeletonFrame frame = e.OpenSkeletonFrame())
        {
            if (frame == null) return;

            if (Skeletons == null || Skeletons.Length != frame.SkeletonArrayLength)
                Skeletons = new Skeleton[frame.SkeletonArrayLength];

            frame.CopySkeletonDataTo(Skeletons);

            var trackedSkeletons = Skeletons
                .Where(s => s != null && s.TrackingState == SkeletonTrackingState.Tracked)
                .Take(2)
                .ToArray();

            lock (_playersLock)
            {
                players.Update(trackedSkeletons);
            }
        }
    }

    public void CloseKinect()
    {
        try
        {
            if (kinectsensor != null)
            {
                if (kinectsensor.IsRunning)
                    kinectsensor.Stop();

                // 이벤트 해제 → 메모리 누수 방지
                kinectsensor.ColorFrameReady -= KinectSensor_ColorFrameReady;
                kinectsensor.DepthFrameReady -= KinectSensor_DepthFrameReady;
                kinectsensor.SkeletonFrameReady -= KinectSensor_SkeletonFrameReady;

                kinectsensor = null;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Kinect 종료 중 오류: " + ex.Message);
        }
    }

    //인식된 스켈레톤 갯수
    public int CountSkeletons()
    {
        return Skeletons?.Length ?? 0;
    }

    //스켈레톤 1번, 스켈레톤 2번이 존재하는가?
    public bool IsPlayer1Detected()
    {
        lock (_playersLock) return players.Player1 != null;
    }
    public bool IsPlayer2Detected()
    {
        lock (_playersLock) return players.Player2 != null;
    }

    public bool ComparePlayers()
    {
        lock (_playersLock)
        {
            var v1 = players.Player1Vector;
            var v2 = players.Player2Vector;

            if (v1 == null || v2 == null) return false;
            if (v1.Length != v2.Length) return false;

            double sum = 0;
            int validCount = 0;

            for (int i = 0; i < v1.Length; i++)
            {
                var a = v1[i];
                var b = v2[i];

                // 유효성 판단: 길이 0이면 무시
                if (a.Length == 0 || b.Length == 0) continue;

                sum += CosineSimilarity(a, b);
                validCount++;
            }

            if (validCount == 0) return false;

            double similarity = sum / validCount;
            return similarity >= 0.65;
        }
    }

    double CosineSimilarity(Vector3D a, Vector3D b)
    {
        double dot = Vector3D.DotProduct(a, b);
        double mag = a.Length * b.Length;

        if (mag == 0) return 0;
        return dot / mag;
    }
}
