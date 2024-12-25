using Dapper;
using Hangfire;
using Npgsql;
using SpeedTest_CN.Models;
using System;
using System.Data;
using System.Data.Common;
using static SpeedTest_CN.SpeedTestHelper;

namespace SpeedTest_CN
{
    public class HangFireHelper
    {
        private readonly IDbConnection _DbConnection;
        private readonly IConfiguration _Configuration;
        public HangFireHelper(IDbConnection DbConnection, IConfiguration configuration)
        {
            _DbConnection = DbConnection;
            _Configuration = configuration;
        }
        public void StartHangFireTask()
        {
            //每日零点0 0 0 */1 * ?
            //每小时0 0 * * * ?
            //每五分钟0 0/5 * * * ?
            RecurringJob.AddOrUpdate("SpeedTest", () => SpeedTest(), "0 0 */1 * * ?", new RecurringJobOptions() { TimeZone = TimeZoneInfo.Local, });
        }
        public void SpeedTest()
        {
            //int TodayRecord = _DbConnection.Query<int>($"select count(0) from speedrecord where to_char(created_at,'yyyy-mm-dd') = '{DateTime.Now.ToString("yyyy-MM-dd")}' ").FirstOrDefault();
            //if (TodayRecord == 0)
            //{
            try
            {
                SpeedTestHelper speedTestHelper = new SpeedTestHelper();
                Server speedResult = speedTestHelper.StartSpeedTest();
                _DbConnection.Execute($@"INSERT INTO speedrecord
                                                            (id,
                                                            ping,
                                                            download, 
                                                            upload, 
                                                            server_id, 
                                                            server_host, 
                                                            server_name, 
                                                            url, 
                                                            scheduled, 
                                                            failed)
                                                            VALUES('{Guid.NewGuid().ToString()}',
                                                                   '{speedResult.Latency}', 
                                                                   {speedResult.downloadSpeed},
                                                                   {speedResult.uploadSpeed}, 
                                                                   {speedResult.Id},
                                                                   '{speedResult.Host}',
                                                                   '{speedResult.Name}', 
                                                                   '{speedResult.Url}',
                                                                   0, 
                                                                   0)");
                if (!string.IsNullOrEmpty(_Configuration["PushMessageUrl"]))
                {
                    PushMessage(speedResult);
                }
            }
            catch (Exception)
            {

            }
            // }
            //using var ExtractionInuLogConn = GetConnection("User ID=productgis;Password=hdkj;Host=192.168.88.31;Port=5432;Database=lunch;Pooling=true;CommandTimeout=1200;");
            //List<lunchInfo> RecoedList = ExtractionInuLogConn.Query<lunchInfo>($@"select  lunchname,count(0) as TotalCount from lunchrecord where yearflag = '{DateTime.Now.Year}' group by lunchname order by TotalCount desc").ToList();
        }

        public void PushMessage(Server speedResult)
        {
            string url = _Configuration["PushMessageUrl"];
            using (HttpClient client = new HttpClient())
            {
                // 创建 MultipartFormDataContent
                using (var formData = new MultipartFormDataContent())
                {
                    // 添加表单字段
                    formData.Add(new StringContent(_Configuration["PushMessageTitle"].ToString()), "title");
                    formData.Add(new StringContent(_Configuration["PushMessageContent"].ToString().Replace("downloadSpeed", speedResult.downloadSpeed.ToString()).Replace("uploadSpeed", speedResult.uploadSpeed.ToString()).Replace("Latency", speedResult.Latency.ToString())), "message");
                    formData.Add(new StringContent(_Configuration["PushMessagePriority"].ToString()), "priority");

                    try
                    {
                        // 发送 POST 请求
                        HttpResponseMessage response = client.PostAsync(url, formData).Result;

                        // 确保请求成功
                        response.EnsureSuccessStatusCode();

                        // 输出返回的内容
                        string responseBody = response.Content.ReadAsStringAsync().Result;
                        Console.WriteLine("Response: " + responseBody);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }
            }
        }
    }
}
