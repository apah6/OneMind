using Microsoft.Kinect;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public class Recognize
{
    private KinectSensor kinectsensor;

    // Color 스트림용
    private WriteableBitmap colorBitmap;
    private byte[] colorPixels;

    // Depth 스트림용
    private WriteableBitmap depthBitmap;
    private short[] depthPixels;
    private byte[] depthColorPixels;

    // Skeleton 스트림용
    public Skeleton[] Skeletons;

    public KinectSensor Sensor => kinectsensor;
    public WriteableBitmap ColorBitmap => colorBitmap;
    public WriteableBitmap DepthBitmap => depthBitmap;

    public Recognize()
    {
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
            frame.CopySkeletonDataTo(Skeletons);
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
}
