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
using System.Windows.Shapes;
using System.Data;
using System.Data.SqlClient;

namespace OneMind
{
    
    public partial class Onemind_record : Window
    {
        private readonly string connectionString = @"\Server=localhost\\SQLEXPRESS;Database=TestDB;Trusted_Connection=True;\";
        public class Result
        {
            public string UserID { get; set; }
            public int CategoryID { get; set; } // DB의 Category_ID에 대응 (PascalCase)
            public int Score { get; set; } // DB의 Game_Word에 대응 (PascalCase)
        }
        public Onemind_record()
        {
            InitializeComponent();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // ReadUserData 함수를 호출하여 데이터를 로드합니다.
            ReadUserData();
        }

        private void ReadUserData()
        {
            List<Result> userList = new List<Result>();

            // using 블록을 사용하여 SqlConnection을 안전하게 닫고 리소스를 해제합니다.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    // 1. 데이터베이스 연결 시도
                    connection.Open();

                    // 2. SQL 쿼리 정의: 이미지에서 확인된 정확한 컬럼 이름(Word_ID, Category_ID, Game_Word)을 사용합니다.
                    string sql = "SELECT * FROM GAME_RESULT";

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            // 3. DataReader를 통해 데이터 한 줄씩 읽기
                            while (reader.Read())
                            {
                                userList.Add(new Result
                                {
                                    // 주의: GetInt32/GetString(인덱스)는 SELECT 문의 열 순서와 일치해야 합니다.
                                    UserID = reader.GetString(0),
                                    CategoryID = reader.GetInt32(1),
                                    Score = reader.GetInt32(2)
                                });
                            }
                        }
                    }

                    // 4. DataGrid에 데이터 목록 바인딩 (XAML의 dataGridUsers 참조)
                    ResultDataGrid.ItemsSource = userList;
                }
                catch (Exception ex)
                {
                    // 연결 오류나 쿼리 실행 오류 발생 시 사용자에게 메시지 표시
                    MessageBox.Show($"데이터베이스 조회 중 오류 발생: {ex.Message}\n\n연결 문자열을 확인하거나 SQL Server에 Tbl_game_word 테이블이 존재하는지 확인하세요.",
                                    "DB 연결 오류",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            base.Close();
        }
    }
}
