using Microsoft.Kinect;
using System;
using System.Linq;
using System.Windows;

public class Recognize //사용할때 Recognize recog = new Recognize();

{
    private KinectSensor kinectsensor;

    public KinectSensor Sensor //사용할때 KinectSensor sensor = recog.Sensor;
    {
        get { return kinectsensor; }
    }

    public Recognize()
    {
        InitializeKinect();
    }

    private void InitializeKinect()
    {
        try
        {
            kinectsensor = KinectSensor.KinectSensors.FirstOrDefault(s => s.Status == KinectStatus.Connected);

            if (kinectsensor != null)
            {
                kinectsensor.Start();
                MessageBox.Show("Kinect 연결 성공!");
            }
            else
            {
                MessageBox.Show("Kinect를 찾을 수 없습니다.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Kinect 초기화 중 오류 발생: " + ex.Message);
        }
    }

    public void CloseKinect()
    {
        if (kinectsensor != null && kinectsensor.IsRunning)
        {
            kinectsensor.Stop();
        }
    }

}
