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
            Window1 gameWindow = new Window1(); // 새 창 객체 생성
            gameWindow.Show();                        // 새 창 띄우기
            this.Hide();
        }
    }
}
