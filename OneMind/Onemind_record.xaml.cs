using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls; // DataGrid 사용을 위해 필요할 수 있습니다.
using System.Data.SqlClient;

namespace OneMind
{
    // XAML에서 정의한 윈도우 이름과 동일해야 합니다.
    public partial class Onemind_record : Window
    {
        // 데이터베이스 연결 문자열
        private string _connStr = @"Server=localhost\SQLEXPRESS;Database=TestDB;Trusted_Connection=True;";

        // DataGrid에 바인딩할 데이터 모델
        public class Result
        {
            public string UserID { get; set; }     // DB: User_ID
            public int CategoryID { get; set; }    // DB: Category_ID
            public int Score { get; set; }         // DB: Score
            public DateTime PlayDate { get; set; } // DB: Play_Date 추가
        }

        // DataGrid의 x:Name을 ResultDataGrid로 가정합니다. 
        // XAML에서 다른 이름을 사용했다면 아래 코드의 ResultDataGrid를 수정하세요.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Readonly 필드를 private로 변경", Justification = "<보류 중>")]
        //internal System.Windows.Controls.DataGrid ResultDataGrid;

        public Onemind_record()
        {
            InitializeComponent();
            // XAML에서 ResultDataGrid를 정의했다면 여기서 찾아 할당합니다.
            // ResultDataGrid = (DataGrid)FindName("ResultDataGrid");

            // 윈도우가 로드된 후 데이터 로딩 함수 호출
            this.Loaded += Onemind_record_Loaded;
        }

        // MainWindow_Loaded 대신 클래스 이름에 맞게 Onemind_record_Loaded로 이름을 변경했습니다.
        private void Onemind_record_Loaded(object sender, RoutedEventArgs e)
        {
            ReadUserData();
        }

        private void ReadUserData()
        {
            List<Result> userList = new List<Result>();

            using (SqlConnection connection = new SqlConnection(_connStr))
            {
                try
                {
                    // 1. 데이터베이스 연결 시도
                    connection.Open();

                    // 2. SQL 쿼리 정의: Play_Date를 포함한 필요한 모든 컬럼을 명시합니다.
                    string sql = @"
                        SELECT User_ID, Category_ID, Score, Play_Date 
                        FROM GAME_RESULT 
                        ORDER BY Score desc, Result_ID";

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            // 3. DataReader를 통해 데이터 한 줄씩 읽기
                            while (reader.Read())
                            {
                                userList.Add(new Result
                                {
                                    // SELECT 쿼리의 순서(0, 1, 2, 3)와 데이터 타입이 일치해야 합니다.
                                    UserID = reader.GetString(0),
                                    CategoryID = reader.GetInt32(1),
                                    Score = reader.GetInt32(2),
                                    PlayDate = reader.GetDateTime(3)
                                });
                            }
                        }
                    }

                    // 4. DataGrid에 데이터 목록 바인딩
                    if (ResultDataGrid != null)
                    {
                        ResultDataGrid.ItemsSource = userList;
                    }
                    else
                    {
                        MessageBox.Show("XAML에서 DataGrid 컨트롤을 찾을 수 없습니다. 이름이 'ResultDataGrid'인지 확인하거나 XAML을 수정해주세요.", "바인딩 오류");
                    }
                }
                catch (Exception ex)
                {
                    // 연결 오류나 쿼리 실행 오류 발생 시 사용자에게 메시지 표시
                    MessageBox.Show($"데이터베이스 조회 중 오류 발생: {ex.Message}\n\n연결 문자열 및 테이블/컬럼 존재 여부를 확인하세요.",
                                    "DB 연결 오류",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }
        }

        // 버튼 클릭 이벤트: 메인 윈도우로 이동
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // MainWindow 객체는 사용자의 초기 윈도우 이름에 따라 다를 수 있습니다.
            // 만약 시작 창 이름이 MainWindow가 아니라면 해당 이름으로 변경하세요.
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            // 현재 창 닫기
            base.Close();
        }
    }
}