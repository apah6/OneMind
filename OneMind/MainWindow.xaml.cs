using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace OneMind
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //testcode
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Kinect 센서가 연결되어 있는지 확인 (일단 주석)
            //if (KinectSensor.KinectSensors.Count == 0) // Kinect 센서가 없는 경우
            //{
            //    MessageBox.Show("Kinect 센서를 찾을 수 없습니다.");
            //    return; // 창 생성 안 함
            //}

            Window1 gameWindow = new Window1(); // 새 창 객체 생성
            gameWindow.Show();                  // 새 창 띄우기
            this.Hide();                        // 메인창 숨기기
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            base.Close(); // 메인 창 닫기
        }
    }
}
