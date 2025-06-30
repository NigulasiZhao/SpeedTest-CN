using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SpeedTest_CN.Models;
using SpeedTest_CN.SpeedTest;
using System.Data;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using SpeedTest_CN.Models.Attendance;
using static SpeedTest_CN.SpeedTestHelper;

namespace SpeedTest_CN.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class SpeedTestController : Controller
{
    private readonly IConfiguration _Configuration;
    private static readonly MemoryCache Cache = new(new MemoryCacheOptions());
    private readonly ILogger<SpeedTestController> _logger;

    public SpeedTestController(IConfiguration configuration, ILogger<SpeedTestController> logger)
    {
        _Configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public string Index()
    {
        var speedTestHelper = new SpeedTestHelper();
        var speedResult = speedTestHelper.StartSpeedTest();
        return string.Format("下载速度: {0} Mbps;  上传速度: {1} Mbps", speedResult.downloadSpeed, speedResult.uploadSpeed);
        ;
    }

    [HttpGet]
    public ActionResult latest()
    {
        IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
        var speedRecord = _DbConnection.Query<SpeedRecord>("select * from speedrecord order by created_at desc").First();
        _DbConnection.Dispose();
        var speedRecordResponse = new SpeedRecordResponse();
        speedRecordResponse.Message = "ok";
        speedRecordResponse.Data = speedRecord;
        // 返回结果
        return Json(speedRecordResponse);
    }

    [HttpGet]
    public ActionResult test()
    {
        // 返回结果
        return Json(new
        {
            id = 1,
            name = "Rick Sanchez",
            status = "Alive",
            species = "Human",
            gender = "Male",
            origin = new
            {
                name = "Earth (C-137)"
            },
            locations = new[]
            {
                new { name = "Earth (C-137)" },
                new { name = "Citadel of Ricks" }
            }
        });
    }

    // [HttpGet]
    // public string CheckInWarning()
    // {
    //     try
    //     {
    //         //if (DateTime.Today.DayOfWeek != DayOfWeek.Saturday && DateTime.Today.DayOfWeek != DayOfWeek.Sunday) return;
    //         _logger.LogInformation("DateTime.Today.DayOfWeek:" + DateTime.Today.DayOfWeek);
    //         var pushMessage = "";
    //         const string fakeSignature = "Gj0IbFZe_rpj5mtMfwoHVo2luGHlmaJa7MtbxwfNSaI";
    //         var listOfPersonnel = _Configuration.GetSection("ListOfPersonnel").Get<List<ListOfPersonnel>>();
    //         if (listOfPersonnel != null)
    //         {
    //             _logger.LogInformation("listOfPersonnel.Count:" + listOfPersonnel.Count);
    //             var realNameList = listOfPersonnel.Select(e => e.RealName).ToList();
    //             _logger.LogInformation("listOfPersonnel.Count:" + listOfPersonnel.Count);
    //             using var sr = new StreamReader(AppDomain.CurrentDomain.BaseDirectory + "AddressBook.json", Encoding.UTF8);
    //             var content = sr.ReadToEnd();
    //             var addressBookList = JsonConvert.DeserializeObject<List<AddressBookInfo>>(content);
    //             _logger.LogInformation("addressBookList.Count:" + addressBookList?.Count);
    //             addressBookList = addressBookList?.Where(e => realNameList.Contains(e.Name)).ToList();
    //             if (addressBookList == null) return "通讯录未格式化";
    //             var header = new
    //             {
    //                 typ = "JWT",
    //                 alg = "HS256"
    //             };
    //             var headerJson = JsonConvert.SerializeObject(header);
    //             var headerBase64 = Base64UrlEncode(headerJson);
    //             var startDate = DateTime.Now;
    //             _logger.LogInformation("startDate:" + startDate);
    //             _logger.LogInformation("addressBookList详情:" + JsonConvert.SerializeObject(addressBookList));
    //             foreach (var addressBookItem in addressBookList)
    //             {
    //                 _logger.LogInformation("开始关键人物循环:");
    //                 var cacheKey = $"CheckInWarned:{addressBookItem.Id}";
    //                 _logger.LogInformation("cacheKey:" + cacheKey);
    //                 if (Cache.TryGetValue(cacheKey, out _)) continue;
    //                 _logger.LogInformation("cache读取完毕");
    //                 var payload = new
    //                 {
    //                     iat = 1734922017,
    //                     id = addressBookItem.Id,
    //                     jwtId = "2318736ce27645c39729dd6cbf6e3232",
    //                     uid = addressBookItem.Id,
    //                     tenantId = "5d89917712441d7a5073058c",
    //                     cid = "5d89917712441d7a5073058c",
    //                     mainId = addressBookItem.MainId,
    //                     avatar = addressBookItem.Avatar,
    //                     name = addressBookItem.Name,
    //                     account = addressBookItem.Sn,
    //                     mobile = addressBookItem.Mobile,
    //                     sn = addressBookItem.Sn,
    //                     group = "6274c1d256a7b338c43fb328",
    //                     groupName = "04.管网管理产线",
    //                     yhloNum = addressBookItem.YhloNum,
    //                     isAdmin = false,
    //                     channel = "app",
    //                     roles = new[]
    //                     {
    //                         "6479adb956a7b33dbcce610c",
    //                         "1826788029153456129",
    //                         "6332ce1b56a7b316e0574808",
    //                         "1775433892067655682",
    //                         "1749600164359757825"
    //                     },
    //                     company = new
    //                     {
    //                         id = "6274c1d256a7b338c43fb328",
    //                         name = "04.管网管理产线",
    //                         code = "647047646"
    //                     },
    //                     tokenfrom = "uniwim",
    //                     userType = "user",
    //                     exp = 1735008477
    //                 };
    //                 _logger.LogInformation("payload拼接文成:");
    //                 var payloadJson = JsonConvert.SerializeObject(payload);
    //                 _logger.LogInformation("payloadJson:" + payloadJson);
    //                 var payloadBase64 = Base64UrlEncode(payloadJson);
    //                 _logger.LogInformation("payloadBase64:" + payloadBase64);
    //                 var jwt = $"{headerBase64}.{payloadBase64}.{fakeSignature}";
    //                 _logger.LogInformation("jwt伪造完成:" + jwt);
    //                 var client = new HttpClient();
    //                 client.DefaultRequestHeaders.Add("Authorization", jwt);
    //                 var response = client.GetAsync("http://122.225.71.14:10001/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" + startDate.ToString("yyyy-MM")).Result;
    //                 var result = response.Content.ReadAsStringAsync().Result;
    //                 _logger.LogInformation("响应内容:" + result);
    //                 var resultModel = JsonConvert.DeserializeObject<AttendanceResponse>(result);
    //                 _logger.LogInformation("resultModel:" + resultModel.Code);
    //                 if (resultModel is not { Code: 200 }) continue;
    //                 var dayAttendanceList = resultModel.Data.DayVoList.FirstOrDefault(e => e.Day == 28);
    //                 _logger.LogInformation("DateTime.Now.Day:" + startDate.Day);
    //                 if (dayAttendanceList == null) continue;
    //                 {
    //                     if (dayAttendanceList.DetailList == null) continue;
    //                     _logger.LogInformation("dayAttendanceList.DetailList :" + JsonConvert.SerializeObject(dayAttendanceList.DetailList));
    //                     foreach (var day in dayAttendanceList.DetailList)
    //                     {
    //                         _logger.LogInformation("day.ClockInType:" + day.ClockInType + ";day.ClockInStatus" + day.ClockInStatus);
    //                         switch (day.ClockInType)
    //                         {
    //                             case "0" when day.ClockInStatus == 1 && day.ClockInStatus != 999:
    //                                 pushMessage += listOfPersonnel.FirstOrDefault(e => e.RealName == addressBookItem.Name)?.FlowerName + "-上班时间:" +
    //                                                DateTime.Parse(day.ClockInTime).ToString("HH:mm:ss") + "\n";
    //                                 Cache.Set(cacheKey, true, new MemoryCacheEntryOptions
    //                                 {
    //                                     AbsoluteExpiration = DateTime.Today.AddDays(1).AddSeconds(-1)
    //                                 });
    //                                 break;
    //                         }
    //                     }
    //                 }
    //             }
    //         }
    //
    //         if (string.IsNullOrEmpty(pushMessage)) return "未见异常信息";
    //         var barkclient = new HttpClient();
    //         var barkUrl = _Configuration.GetSection("BarkUrl").Get<string>();
    //         var data = JsonConvert.SerializeObject(new
    //         {
    //             body = pushMessage,
    //             title = "高危人员打卡提醒",
    //             badge = 1,
    //             icon = "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/netcam-studio.png",
    //             group = ""
    //         });
    //         var byteContent = new ByteArrayContent(Encoding.UTF8.GetBytes(data));
    //         byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    //         barkclient.PostAsync(barkUrl, byteContent);
    //         return "成功";
    //     }
    //     catch (Exception e)
    //     {
    //         return e.Message;
    //     }
    // }
    //
    // private static string Base64UrlEncode(string input)
    // {
    //     var bytes = Encoding.UTF8.GetBytes(input);
    //     return Convert.ToBase64String(bytes)
    //         .TrimEnd('=')
    //         .Replace('+', '-')
    //         .Replace('/', '_');
    // }
}