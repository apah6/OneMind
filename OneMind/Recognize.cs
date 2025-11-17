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

    // 이벤트: 플레이어별 최신 크롭 이미지를 UI로 보냄
    public event Action<WriteableBitmap, WriteableBitmap> PlayerCropsUpdated;

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

            frame.CopyPixelDataTo(colorPixels);

            // UI 스레드 안전하게 업데이트
            Application.Current.Dispatcher.Invoke(() =>
            {
                colorBitmap.WritePixels(
                    new Int32Rect(0, 0, frame.Width, frame.Height),
                    colorPixels,
                    frame.Width * sizeof(int),
                    0);
            });
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

            // 플레이어 크롭을 얻어 UI에 푸시 (null 가능한 항목 허용)
            var crops = GetBothPlayerColorCrops(20);
            // UI 스레드에서 이벤트 발생 (비동기)
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                PlayerCropsUpdated?.Invoke(crops[0], crops[1]);
            }));
        }
    }

    // --- 추가된 메서드: 플레이어 기준으로 color 프레임을 크롭해서 반환 ---
    // playerIndex: 1 또는 2 (존재하지 않으면 null 반환)
    // padding: 바운딩 박스 주변 여백 (픽셀)
    public WriteableBitmap GetPlayerColorCrop(int playerIndex, int padding = 20)
    {
        JointCollection joints;
        lock (_playersLock)
        {
            if (players == null) return null;
            joints = playerIndex == 1 ? players.Player1 : players.Player2;
        }

        if (joints == null) return null;
        if (kinectsensor == null || colorBitmap == null || colorPixels == null) return null;

        int frameW = colorBitmap.PixelWidth;
        int frameH = colorBitmap.PixelHeight;

        int minX = frameW, minY = frameH, maxX = 0, maxY = 0;
        bool any = false;

        // JointCollection은 IEnumerable<Joint>를 제공하므로 foreach 사용
        foreach (Joint j in joints)
        {
            if (j == null) continue;
            // 좌표 매핑 (Color 해상도와 동일한 포맷 사용)
            var pt = kinectsensor.CoordinateMapper.MapSkeletonPointToColorPoint(j.Position, ColorImageFormat.RgbResolution640x480Fps30);
            // Map 결과가 음수이거나 프레임을 벗어날 수 있으니 클램프는 나중에 처리
            minX = Math.Min(minX, pt.X);
            minY = Math.Min(minY, pt.Y);
            maxX = Math.Max(maxX, pt.X);
            maxY = Math.Max(maxY, pt.Y);
            any = true;
        }

        if (!any) return null;

        minX = Math.Max(0, minX - padding);
        minY = Math.Max(0, minY - padding);
        maxX = Math.Min(frameW - 1, maxX + padding);
        maxY = Math.Min(frameH - 1, maxY + padding);

        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        if (w <= 0 || h <= 0) return null;

        int srcStride = frameW * 4;
        int dstStride = w * 4;
        byte[] crop = new byte[w * h * 4];

        // 안전하게 블록 복사 (행 단위)
        for (int row = 0; row < h; row++)
        {
            int srcIndex = ((minY + row) * frameW + minX) * 4;
            int dstIndex = row * dstStride;
            Buffer.BlockCopy(colorPixels, srcIndex, crop, dstIndex, dstStride);
        }

        var wb = new WriteableBitmap(w, h, 96.0, 96.0, PixelFormats.Bgr32, null);
        wb.WritePixels(new Int32Rect(0, 0, w, h), crop, dstStride, 0);
        return wb;
    }

    // 두 플레이어의 크롭 이미지를 동시에 얻기 (존재하지 않으면 해당 인덱스에 null)
    public WriteableBitmap[] GetBothPlayerColorCrops(int padding = 20)
    {
        return new WriteableBitmap[]
        {
            GetPlayerColorCrop(1, padding),
            GetPlayerColorCrop(2, padding)
        };
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
