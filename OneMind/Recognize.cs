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
            //vdsdsdsd
        }

    public void CloseKinect()
    {
        if (kinectsensor != null && kinectsensor.IsRunning)
        {
            kinectsensor.Stop();
        }
    }

}
