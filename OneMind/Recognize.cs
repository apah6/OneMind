using Microsoft.Kinect;
using System;
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
            var sensors = KinectSensor.KinectSensors;
            if (sensors.Count > 0)
            {
                kinectsensor = sensors[0];
                if (kinectsensor.Status == KinectStatus.Connected)
                {
                    kinectsensor.Start();
                }
                else
                {
                    MessageBox.Show("Kinect가 연결되어 있지 않습니다.");
                }
            }
            else
            {
                MessageBox.Show("Kinect 센서를 찾을 수 없습니다.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("키넥트 초기화 오류입니다.: " + ex.Message);
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
