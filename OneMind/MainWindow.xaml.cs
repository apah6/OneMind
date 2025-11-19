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
using System.Data;
using System.Data.SqlClient;
using Microsoft.Kinect;


namespace OneMind
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //testcode
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Recognize 인스턴스를 메인에서 생성해서 Window1에 주입(전달)
            Recognize recognizer = new Recognize();

            // 사용자 입력/선택 값 가져오기
            string teamName = NicknameBox.Text.Trim();
            string categoryName = (CategoryBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrWhiteSpace(NicknameBox.Text))
            {
                MessageBox.Show("닉네임을 입력하세요!", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;  // 진행 중단
            }

            if (string.IsNullOrEmpty(categoryName))
            {
                MessageBox.Show("카테고리를 선택하세요.");
                return;
            }

            Window1 gameWindow = new Window1(recognizer, teamName, categoryName); // 생성자 주입
            gameWindow.Show();
            this.Hide();                        // 메인창 숨기기
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            base.Close(); // 메인 창 닫기
        }
    }
}
