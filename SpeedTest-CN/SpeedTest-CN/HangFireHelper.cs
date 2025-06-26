using Dapper;
using Hangfire;
using Newtonsoft.Json;
using Npgsql;
using SpeedTest_CN.Models;
using SpeedTest_CN.Models.Attendance;
using SpeedTest_CN.Models.EventInfo;
using System.Data;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace SpeedTest_CN;

public class HangFireHelper(IConfiguration configuration)
{
    public void StartHangFireTask()
    {
        //每日零点0 0 0 */1 * ?
        //每小时0 0 * * * ?
        //每五分钟0 0/5 * * * ?
        //RecurringJob.AddOrUpdate("SpeedTest", () => SpeedTest(), "0 0 */1 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("AttendanceRecord", () => AttendanceRecord(), "0 0 */3 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("KeepRecord", () => KeepRecord(), "0 0 */3 * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        RecurringJob.AddOrUpdate("CheckInWarning", () => CheckInWarning(), "0 0/10 * * * ?", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
    }

    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void SpeedTest()
    {
        try
        {
            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            var speedTestHelper = new SpeedTestHelper();
            var speedResult = speedTestHelper.StartSpeedTest();
            dbConnection.Execute($"""
                                  INSERT INTO speedrecord
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
                                                                                                     0)
                                  """);
            if (!string.IsNullOrEmpty(configuration["PushMessageUrl"])) PushMessage(speedResult);
            dbConnection.Dispose();
        }
        catch (Exception)
        {
            // ignored
        }
        // }
        //using var ExtractionInuLogConn = GetConnection("User ID=productgis;Password=hdkj;Host=192.168.88.31;Port=5432;Database=lunch;Pooling=true;CommandTimeout=1200;");
        //List<lunchInfo> RecoedList = ExtractionInuLogConn.Query<lunchInfo>($@"select  lunchname,count(0) as TotalCount from lunchrecord where yearflag = '{DateTime.Now.Year}' group by lunchname order by TotalCount desc").ToList();
    }

    public void PushMessage(Server speedResult)
    {
        var url = configuration["PushMessageUrl"];
        using var client = new HttpClient();
        // 创建 MultipartFormDataContent
        using var formData = new MultipartFormDataContent();
        // 添加表单字段
        formData.Add(new StringContent(configuration["PushMessageTitle"]!.ToString()), "title");
        formData.Add(
            new StringContent(configuration["PushMessageContent"]!.ToString().Replace("downloadSpeed", speedResult.downloadSpeed.ToString()).Replace("uploadSpeed", speedResult.uploadSpeed.ToString())
                .Replace("Latency", speedResult.Latency.ToString())), "message");
        formData.Add(new StringContent(configuration["PushMessagePriority"]!.ToString()), "priority");

        try
        {
            // 发送 POST 请求
            var response = client.PostAsync(url, formData).Result;

            // 确保请求成功
            response.EnsureSuccessStatusCode();

            // 输出返回的内容
            var responseBody = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine("Response: " + responseBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    public void AttendanceRecord()
    {
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", configuration["yinuotoken"]);
        var startDate = DateTime.Now.AddDays(-1);
        var response = client.GetAsync("http://122.225.71.14:10001/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" + startDate.ToString("yyyy-MM")).Result;
        var result = response.Content.ReadAsStringAsync().Result;
        var resultModel = JsonConvert.DeserializeObject<AttendanceResponse>(result);
        if (resultModel is { Code: 200 })
        {
            dbConnection.Execute($"delete from public.attendancerecord where attendancemonth = '{startDate:yyyy-MM}'");
            dbConnection.Execute($"delete from public.attendancerecordday where to_char(attendancedate,'yyyy-mm') = '{startDate:yyyy-MM}'");
            dbConnection.Execute($"delete from public.attendancerecorddaydetail where to_char(attendancedate,'yyyy-mm') = '{startDate:yyyy-MM}'");
            dbConnection.Execute(
                $"INSERT INTO public.attendancerecord(attendancemonth,workdays,latedays,earlydays) VALUES('{startDate:yyyy-MM}',{resultModel.Data.WorkDays},{resultModel.Data.LateDays},{resultModel.Data.EarlyDays});");
            foreach (var item in resultModel.Data.DayVoList)
            {
                var flagedate = DateTime.Parse(startDate.ToString("yyyy-MM") + "-" + item.Day);
                if (item.WorkHours == null) continue;
                dbConnection.Execute($"""
                                      INSERT INTO public.attendancerecordday(untilthisday,day,checkinrule,isnormal,isabnormal,isapply,clockinnumber,workhours,attendancedate)
                                                                                              VALUES({item.UntilThisDay},{item.Day},'{item.CheckInRule}','{item.IsNormal}','{item.IsAbnormal}','{item.IsApply}',{item.ClockInNumber},{item.WorkHours},to_timestamp('{flagedate:yyyy-MM-dd 00:00:00}', 'yyyy-mm-dd hh24:mi:ss'));
                                      """);
                if (item.DetailList != null)
                    foreach (var daydetail in item.DetailList)
                        dbConnection.Execute($"""
                                              INSERT INTO public.attendancerecorddaydetail(id,recordid,clockintype,clockintime,attendancedate)
                                                                                                      VALUES({daydetail.Id},{daydetail.RecordId},'{daydetail.ClockInType}',to_timestamp('{daydetail.ClockInTime}', 'yyyy-mm-dd hh24:mi:ss'),to_timestamp('{flagedate:yyyy-MM-dd 00:00:00}', 'yyyy-mm-dd hh24:mi:ss'));
                                              """);
            }
        }

        dbConnection.Dispose();
    }

    public void KeepRecord()
    {
        #region V2

        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("x-bundleId", configuration["KEEPx-bundleId"]);
        client.DefaultRequestHeaders.Add("x-session-id", configuration["KEEPx-session-id"]);
        client.DefaultRequestHeaders.Add("Cookie", configuration["KEEPCookie"]);
        client.DefaultRequestHeaders.Add("x-user-id", configuration["KEEPx-user-id"]);
        client.DefaultRequestHeaders.Add("x-keep-timezone", configuration["KEEPx-keep-timezone"]);
        client.DefaultRequestHeaders.Add("Authorization", configuration["KEEPAuthorization"]);
        var response = client.GetAsync("https://api.gotokeep.com/pd/v3/stats/detail?dateUnit=all").Result;
        var result = response.Content.ReadAsStringAsync().Result;
        var resultModel = JsonConvert.DeserializeObject<KeepResponse>(result);
        if (resultModel is { ok: true })
            foreach (var logitem in resultModel.data.records.SelectMany(item => item.logs))
            {
                if (logitem.stats == null) continue;
                dbConnection.Execute($@"delete from public.eventinfo where source = :source and distinguishingmark=:distinguishingmark",
                    new { source = "keep", distinguishingmark = logitem.stats.id });
                if (logitem.stats.type != "training")
                {
                    // 转换为TimeSpan 
                    var span = TimeSpan.FromMilliseconds(Math.Abs(logitem.stats.endTime - logitem.stats.startTime));
                    dbConnection.Execute(
                        $@"INSERT INTO public.eventinfo(id,title,message,clockintime,color,source,distinguishingmark) VALUES(:id,:title,:message,to_timestamp(:clockintime, 'yyyy-mm-dd hh24:mi:ss'),:color,:source,:distinguishingmark);"
                        , new
                        {
                            id = Guid.NewGuid().ToString(),
                            title = logitem.stats.name + logitem.stats.nameSuffix,
                            message = "用时 " + $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2};消耗 " + logitem.stats.calorie + "千卡",
                            clockintime = DateTime.Parse(logitem.stats.doneDate).ToString("yyyy-MM-dd HH:mm:ss"),
                            color = "green",
                            source = "keep",
                            distinguishingmark = logitem.stats.id
                        });
                }
                else
                {
                    var traresponse = client.GetAsync("https://api.gotokeep.com/minnow-webapp/v1/sportlog/" + logitem.stats.id).Result;
                    var traresult = traresponse.Content.ReadAsStringAsync().Result;
                    var traResultModel = JsonConvert.DeserializeObject<SportLogResponse>(traresult);
                    if (traResultModel is not { ok: true }) continue;
                    var sportLogSectionsModel = traResultModel.data.sections.FirstOrDefault(e => e.style.ToLower() == "sportdata");
                    if (sportLogSectionsModel != null)
                    {
                        var sportLogContentListTime = sportLogSectionsModel.content.list.FirstOrDefault(e => e.title == "训练时长");
                        var sportLogContentListDistance = sportLogSectionsModel.content.list.FirstOrDefault(e => e.title == "总距离");
                        if (sportLogContentListTime != null && sportLogContentListDistance != null)
                            dbConnection.Execute(
                                $@"INSERT INTO public.eventinfo(id,title,message,clockintime,color,source,distinguishingmark) VALUES(:id,:title,:message,to_timestamp(:clockintime, 'yyyy-mm-dd hh24:mi:ss'),:color,:source,:distinguishingmark);"
                                , new
                                {
                                    id = Guid.NewGuid().ToString(),
                                    title = logitem.stats.name + logitem.stats.nameSuffix + sportLogContentListDistance.valueStr + sportLogContentListDistance.unit,
                                    message = "用时 " + sportLogContentListTime.valueStr + ";消耗 " + logitem.stats.calorie + "千卡",
                                    clockintime = DateTime.Parse(logitem.stats.doneDate).ToString("yyyy-MM-dd HH:mm:ss"),
                                    color = "green",
                                    source = "keep",
                                    distinguishingmark = logitem.stats.id
                                });
                    }
                }
            }

        dbConnection.Dispose();

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

    public void CheckInWarning()
    {
        if (DateTime.Today.DayOfWeek != DayOfWeek.Saturday && DateTime.Today.DayOfWeek != DayOfWeek.Sunday) return;
        var pushMessage = "";
        const string fakeSignature = "Gj0IbFZe_rpj5mtMfwoHVo2luGHlmaJa7MtbxwfNSaI";
        var listOfPersonnel = configuration.GetSection("ListOfPersonnel").Get<List<ListOfPersonnel>>();
        var realNameList = listOfPersonnel.Select(e => e.RealName).ToList();
        using var sr = new StreamReader(AppDomain.CurrentDomain.BaseDirectory + "AddressBook.json");
        var content = sr.ReadToEnd();
        var addressBookList = JsonConvert.DeserializeObject<List<AddressBookInfo>>(content);

        addressBookList = addressBookList?.Where(e => realNameList.Contains(e.Name)).ToList();
        if (addressBookList == null) return;
        var header = new
        {
            typ = "JWT",
            alg = "HS256"
        };
        var headerJson = JsonConvert.SerializeObject(header);
        var headerBase64 = Base64UrlEncode(headerJson);
        var startDate = DateTime.Now;
        foreach (var addressBookItem in addressBookList)
        {
            var cacheKey = $"CheckInWarned:{addressBookItem.Id}:{DateTime.Today:yyyyMMdd}";
            if (_cache.TryGetValue(cacheKey, out _)) continue;

            var payload = new
            {
                iat = 1734922017,
                id = addressBookItem.Id,
                jwtId = "2318736ce27645c39729dd6cbf6e3232",
                uid = addressBookItem.Id,
                tenantId = "5d89917712441d7a5073058c",
                cid = "5d89917712441d7a5073058c",
                mainId = addressBookItem.MainId,
                avatar = addressBookItem.Avatar,
                name = addressBookItem.Name,
                account = addressBookItem.Sn,
                mobile = addressBookItem.Mobile,
                sn = addressBookItem.Sn,
                group = "6274c1d256a7b338c43fb328",
                groupName = "04.管网管理产线",
                yhloNum = addressBookItem.YhloNum,
                isAdmin = false,
                channel = "app",
                roles = new[]
                {
                    "6479adb956a7b33dbcce610c",
                    "1826788029153456129",
                    "6332ce1b56a7b316e0574808",
                    "1775433892067655682",
                    "1749600164359757825"
                },
                company = new
                {
                    id = "6274c1d256a7b338c43fb328",
                    name = "04.管网管理产线",
                    code = "647047646"
                },
                tokenfrom = "uniwim",
                userType = "user",
                exp = 1735008477
            };
            var payloadJson = JsonConvert.SerializeObject(payload);
            var payloadBase64 = Base64UrlEncode(payloadJson);
            var jwt = $"{headerBase64}.{payloadBase64}.{fakeSignature}";
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", jwt);
            var response = client.GetAsync("http://122.225.71.14:10001/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" + startDate.ToString("yyyy-MM")).Result;
            var result = response.Content.ReadAsStringAsync().Result;
            var resultModel = JsonConvert.DeserializeObject<AttendanceResponse>(result);
            if (resultModel is not { Code: 200 }) continue;
            var dayAttendanceList = resultModel.Data.DayVoList.FirstOrDefault(e => e.Day == DateTime.Now.Day);
            if (dayAttendanceList == null) continue;
            {
                if (dayAttendanceList.DetailList == null) continue;
                foreach (var day in dayAttendanceList.DetailList)
                    switch (day.ClockInType)
                    {
                        case "0" when day.ClockInStatus == 1 && day.ClockInStatus != 999:
                            pushMessage += listOfPersonnel.FirstOrDefault(e => e.RealName == addressBookItem.Name)?.FlowerName + "-上班时间:" +
                                           DateTime.Parse(day.ClockInTime).ToString("HH:mm:ss") + "\n";
                            _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
                            {
                                AbsoluteExpiration = DateTime.Today.AddDays(1).AddSeconds(-1)
                            });
                            break;
                        // case "1" when day.ClockInStatus != 999:
                        //     pushMessage += listOfPersonnel.FirstOrDefault(e => e.RealName == addressBookItem.Name)?.FlowerName + "-签退时间:" +
                        //                    DateTime.Parse(day.ClockInTime).ToString("HH:mm:ss") + ";";
                        //     break;
                    }
            }
        }

        if (string.IsNullOrEmpty(pushMessage)) return;
        var barkclient = new HttpClient();
        var barkUrl = configuration.GetSection("BarkUrl").Get<string>();
        var data = JsonConvert.SerializeObject(new
        {
            body = pushMessage,
            title = "高危人员打卡提醒",
            badge = 1,
            icon = "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/netcam-studio.png",
            group = ""
        });
        var byteContent = new ByteArrayContent(Encoding.UTF8.GetBytes(data));
        byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        barkclient.PostAsync(barkUrl, byteContent);
        if (string.IsNullOrEmpty(configuration["PushMessageUrl"])) return;
        var url = configuration["PushMessageUrl"];
        using var gotifyclient = new HttpClient();
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent("高危人员打卡提醒"), "title");
        formData.Add(new StringContent(pushMessage), "message");
        formData.Add(new StringContent(configuration["PushMessagePriority"]!.ToString()), "priority");
        try
        {
            var response = gotifyclient.PostAsync(url, formData).Result;
            response.EnsureSuccessStatusCode();
            var responseBody = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine("Response: " + responseBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}