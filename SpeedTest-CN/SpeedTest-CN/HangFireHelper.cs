using Dapper;
using Hangfire;
using Newtonsoft.Json;
using Npgsql;
using SpeedTest_CN.Models;
using SpeedTest_CN.Models.Attendance;
using SpeedTest_CN.Models.EventInfo;
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
            RecurringJob.AddOrUpdate("AttendanceRecord", () => AttendanceRecord(), "0 0 */3 * * ?", new RecurringJobOptions() { TimeZone = TimeZoneInfo.Local, });
            RecurringJob.AddOrUpdate("KeepRecord", () => KeepRecord(), "0 0 */3 * * ?", new RecurringJobOptions() { TimeZone = TimeZoneInfo.Local, });
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
        public void KeepRecord()
        {
            #region V2
            IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("x-bundleId", _Configuration["KEEPx-bundleId"]);
            client.DefaultRequestHeaders.Add("x-session-id", _Configuration["KEEPx-session-id"]);
            client.DefaultRequestHeaders.Add("Cookie", _Configuration["KEEPCookie"]);
            client.DefaultRequestHeaders.Add("x-user-id", _Configuration["KEEPx-user-id"]);
            client.DefaultRequestHeaders.Add("x-keep-timezone", _Configuration["KEEPx-keep-timezone"]);
            client.DefaultRequestHeaders.Add("Authorization", _Configuration["KEEPAuthorization"]);
            var response = client.GetAsync("https://api.gotokeep.com/pd/v3/stats/detail?dateUnit=all").Result;
            string result = response.Content.ReadAsStringAsync().Result;
            KeepResponse ResultModel = JsonConvert.DeserializeObject<KeepResponse>(result);
            if (ResultModel.ok)
            {
                foreach (var item in ResultModel.data.records)
                {
                    foreach (var Logitem in item.logs)
                    {
                        if (Logitem.stats != null)
                        {
                            _DbConnection.Execute($@"delete from public.eventinfo where source = :source and distinguishingmark=:distinguishingmark", new { source = "keep", distinguishingmark = Logitem.stats.id });
                            if (Logitem.stats.type != "training")
                            {
                                // 转换为TimeSpan 
                                TimeSpan span = TimeSpan.FromMilliseconds(Math.Abs(Logitem.stats.endTime - Logitem.stats.startTime));
                                _DbConnection.Execute($@"INSERT INTO public.eventinfo(id,title,message,clockintime,color,source,distinguishingmark) VALUES(:id,:title,:message,to_timestamp(:clockintime, 'yyyy-mm-dd hh24:mi:ss'),:color,:source,:distinguishingmark);"
                                          , new
                                          {
                                              id = Guid.NewGuid().ToString(),
                                              title = Logitem.stats.name + Logitem.stats.nameSuffix,
                                              message = "用时 " + $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2};消耗 " + Logitem.stats.calorie + "千卡",
                                              clockintime = DateTime.Parse(Logitem.stats.doneDate).ToString("yyyy-MM-dd HH:mm:ss"),
                                              color = "green",
                                              source = "keep",
                                              distinguishingmark = Logitem.stats.id
                                          });
                            }
                            else
                            {
                                var Traresponse = client.GetAsync("https://api.gotokeep.com/minnow-webapp/v1/sportlog/" + Logitem.stats.id).Result;
                                string Traresult = Traresponse.Content.ReadAsStringAsync().Result;
                                SportLogResponse TraResultModel = JsonConvert.DeserializeObject<SportLogResponse>(Traresult);
                                if (TraResultModel.ok)
                                {
                                    SportLogSections SportLogSectionsModel = TraResultModel.data.sections.FirstOrDefault(e => e.style.ToLower() == "sportdata");
                                    if (SportLogSectionsModel != null)
                                    {
                                        SportLogContentList SportLogContentListTime = SportLogSectionsModel.content.list.FirstOrDefault(e => e.title == "训练时长");
                                        SportLogContentList SportLogContentListDistance = SportLogSectionsModel.content.list.FirstOrDefault(e => e.title == "总距离");
                                        if (SportLogContentListTime != null && SportLogContentListDistance != null)
                                        {
                                            _DbConnection.Execute($@"INSERT INTO public.eventinfo(id,title,message,clockintime,color,source,distinguishingmark) VALUES(:id,:title,:message,to_timestamp(:clockintime, 'yyyy-mm-dd hh24:mi:ss'),:color,:source,:distinguishingmark);"
                                         , new
                                         {
                                             id = Guid.NewGuid().ToString(),
                                             title = Logitem.stats.name + Logitem.stats.nameSuffix + SportLogContentListDistance.valueStr + SportLogContentListDistance.unit,
                                             message = "用时 " + SportLogContentListTime.valueStr + ";消耗 " + Logitem.stats.calorie + "千卡",
                                             clockintime = DateTime.Parse(Logitem.stats.doneDate).ToString("yyyy-MM-dd HH:mm:ss"),
                                             color = "green",
                                             source = "keep",
                                             distinguishingmark = Logitem.stats.id
                                         });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            _DbConnection.Dispose();
            #endregion
            #region V1
            //IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
            //HttpClient client = new HttpClient();
            //client.DefaultRequestHeaders.Add("x-bundleId", _Configuration["KEEPx-bundleId"]);
            //client.DefaultRequestHeaders.Add("x-session-id", _Configuration["KEEPx-session-id"]);
            //client.DefaultRequestHeaders.Add("Cookie", _Configuration["KEEPCookie"]);
            //client.DefaultRequestHeaders.Add("x-user-id", _Configuration["KEEPx-user-id"]);
            //client.DefaultRequestHeaders.Add("x-keep-timezone", _Configuration["KEEPx-keep-timezone"]);
            //client.DefaultRequestHeaders.Add("Authorization", _Configuration["KEEPAuthorization"]);
            //DateTime dtNow = DateTime.Now;
            //var response = client.GetAsync("https://api.gotokeep.com/feynman/v8/data-center/sub/sport-log/card/SPORT_LOG_LIST_CARD?sportType=all&dateUnit=daily&date=" + dtNow.ToString("yyyyMMdd")).Result;
            //string result = response.Content.ReadAsStringAsync().Result;
            //KeepResponse ResultModel = JsonConvert.DeserializeObject<KeepResponse>(result);
            //if (ResultModel.ok)
            //{
            //    foreach (var item in ResultModel.data.data.dailyList)
            //    {
            //        foreach (var Logitem in item.logList)
            //        {
            //            _DbConnection.Execute($@"delete from public.eventinfo where source = :source and distinguishingmark=:distinguishingmark", new { source = "keep", distinguishingmark = Logitem.id });
            //            _DbConnection.Execute($@"INSERT INTO public.eventinfo(id,title,message,clockintime,color,source,distinguishingmark) VALUES(:id,:title,:message,to_timestamp(:clockintime, 'yyyy-mm-dd hh24:mi:ss'),:color,:source,:distinguishingmark);"
            //                           , new
            //                           {
            //                               id = Guid.NewGuid().ToString(),
            //                               title = Logitem.name + Logitem.nameSuffix,
            //                               message = string.Join(';', Logitem.indicatorList),
            //                               clockintime = dtNow.ToString("yyyy-MM-dd") + " " + Logitem.endTimeText + ":00",
            //                               color = "green",
            //                               source = "keep",
            //                               distinguishingmark = Logitem.id
            //                           });
            //        }
            //    }
            //}
            //_DbConnection.Dispose();
            #endregion
        }
    }
}
