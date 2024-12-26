using Dapper;
using Hangfire;
using Newtonsoft.Json;
using Npgsql;
using SpeedTest_CN.Models;
using SpeedTest_CN.Models.Attendance;
using System;
using System.Data;
using System.Data.Common;
using static SpeedTest_CN.SpeedTestHelper;

namespace SpeedTest_CN
{
    public class HangFireHelper
    {
        private readonly IConfiguration _Configuration;
        public HangFireHelper(IConfiguration configuration)
        {
            _Configuration = configuration;
        }
        public void StartHangFireTask()
        {
            //每日零点0 0 0 */1 * ?
            //每小时0 0 * * * ?
            //每五分钟0 0/5 * * * ?
            RecurringJob.AddOrUpdate("SpeedTest", () => SpeedTest(), "0 0 */1 * * ?", new RecurringJobOptions() { TimeZone = TimeZoneInfo.Local, });
            RecurringJob.AddOrUpdate("AttendanceRecord", () => AttendanceRecord(), "0 20 7 * * ?", new RecurringJobOptions() { TimeZone = TimeZoneInfo.Local, });
        }
        public void SpeedTest()
        {
            try
            {
                IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
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
                _DbConnection.Dispose();
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

        public void AttendanceRecord()
        {
            IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", _Configuration["yinuotoken"]);
            DateTime StartDate = DateTime.Now.AddDays(-1);
            var response = client.GetAsync("http://122.225.71.14:10001/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" + StartDate.ToString("yyyy-MM")).Result;
            string result = response.Content.ReadAsStringAsync().Result;
            AttendanceResponse ResultModel = JsonConvert.DeserializeObject<AttendanceResponse>(result);
            if (ResultModel.Code == 200)
            {
                _DbConnection.Execute($@"delete from public.attendancerecord where attendancemonth = '{StartDate.ToString("yyyy-MM")}'");
                _DbConnection.Execute($@"delete from public.attendancerecordday where to_char(attendancedate,'yyyy-mm') = '{StartDate.ToString("yyyy-MM")}'");
                _DbConnection.Execute($@"delete from public.attendancerecorddaydetail where to_char(attendancedate,'yyyy-mm') = '{StartDate.ToString("yyyy-MM")}'");
                _DbConnection.Execute($"INSERT INTO public.attendancerecord(attendancemonth,workdays,latedays,earlydays) VALUES('{StartDate.ToString("yyyy-MM")}',{ResultModel.Data.WorkDays},{ResultModel.Data.LateDays},{ResultModel.Data.EarlyDays});");
                foreach (var item in ResultModel.Data.DayVoList)
                {
                    DateTime flagedate = DateTime.Parse(StartDate.ToString("yyyy-MM") + "-" + item.Day);
                    if (item.WorkHours != null)
                    {
                        _DbConnection.Execute($@"INSERT INTO public.attendancerecordday(untilthisday,day,checkinrule,isnormal,isabnormal,isapply,clockinnumber,workhours,attendancedate)
                                                        VALUES({item.UntilThisDay},{item.Day},'{item.CheckInRule}','{item.IsNormal}','{item.IsAbnormal}','{item.IsApply}',{item.ClockInNumber},{item.WorkHours},to_timestamp('{flagedate.ToString("yyyy-MM-dd 00:00:00")}', 'yyyy-mm-dd hh24:mi:ss'));");
                        if (item.DetailList != null)
                        {
                            foreach (var daydetail in item.DetailList)
                            {
                                _DbConnection.Execute($@"INSERT INTO public.attendancerecorddaydetail(id,recordid,clockintype,clockintime,attendancedate)
                                                        VALUES({daydetail.Id},{daydetail.RecordId},'{daydetail.ClockInType}',to_timestamp('{daydetail.ClockInTime}', 'yyyy-mm-dd hh24:mi:ss'),to_timestamp('{flagedate.ToString("yyyy-MM-dd 00:00:00")}', 'yyyy-mm-dd hh24:mi:ss'));");
                            }
                        }
                    }
                }
            }
            _DbConnection.Dispose();
        }
    }
}
