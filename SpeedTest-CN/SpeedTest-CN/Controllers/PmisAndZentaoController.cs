using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using SpeedTest_CN.Common;
using SpeedTest_CN.Models.PmisAndZentao;

namespace SpeedTest_CN.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class PmisAndZentaoController(IConfiguration configuration, ILogger<SpeedTestController> logger, ZentaoHelper zentaoHelper) : Controller
{
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<SpeedTestController> _logger = logger;

    [Tags("禅道")]
    [EndpointSummary("获取禅道Token(有效期24分钟)")]
    [HttpGet]
    public string GetZentaoToken()
    {
        return zentaoHelper.GetZentaoToken();
    }

    [Tags("禅道")]
    [EndpointSummary("获取我的任务列表")]
    [HttpGet]
    public List<ZentaoTaskItem> GetMyWorkTask()
    {
        return zentaoHelper.GetZentaoTask();
    }

    [Tags("禅道")]
    [EndpointSummary("同步禅道任务")]
    [HttpGet]
    public bool GetZentaoTask()
    {
        return zentaoHelper.SynchronizationZentaoTask();
    }

    [Tags("禅道")]
    [EndpointSummary("完成任务")]
    [HttpGet]
    public string FinishTask(DateTime finishedDate, double totalHours)
    {
        zentaoHelper.FinishZentaoTask(finishedDate, totalHours);
        return "成功";
    }


    [Tags("禅道")]
    [EndpointSummary("计算工时")]
    [HttpGet]
    public List<TaskItem> AllocateWork(DateTime startDate, double totalHours)
    {
        var result = zentaoHelper.AllocateWork(startDate, totalHours);
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("获取已上报列表")]
    [HttpGet]
    public async Task<string> QueryMy()
    {
        var pmisInfo = _configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var postResponse = await httpHelper.PostAsync(pmisInfo.Url + "/unioa/job/userWork/queryMy", new
        {
            index = 1,
            size = 30,
            conditions = new object[] { },
            order = new object[] { },
            data = new
            {
                status = (object)null,
                hasFile = (object)null,
                time = new object[] { }
            }
        }, new Dictionary<string, string> { { "authorization", pmisInfo.Authorization } });
        return await postResponse.Content.ReadAsStringAsync();
    }

    [Tags("PMIS")]
    [EndpointSummary("获取工作明细")]
    [HttpGet]
    public async Task<string> GetByDateAndUserId(string fillDate = "2025-06-27", string userId = "6316c6eb56a7b316e056face")
    {
        var pmisInfo = _configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var getResponse = await httpHelper.GetAsync(pmisInfo.Url + $"/unioa/job/userWork/getByDateAndUserId?fillDate={fillDate}&userId={userId}&type=0",
            new Dictionary<string, string> { { "authorization", pmisInfo.Authorization } });
        var result = JsonSerializer.Deserialize<GetByDateAndUserIdResponse>(getResponse.Content.ReadAsStringAsync().Result);
        if (!result.Success || result.Response == null) return JsonSerializer.Serialize(result);
        result.Response.status = "1";
        foreach (var detailItem in result.Response.details)
        {
            detailItem.target = detailItem.description;
            detailItem.planFinishAct = detailItem.description;
            detailItem.responsibility = "负责基于.NET平台的后端代码开发与单元测试";
            detailItem.workType = "代码开发";
            detailItem.realJob = detailItem.description;
        }

        return JsonSerializer.Serialize(result);
    }

    public static double GetRoundedHours(DateTime actualTime)
    {
        // 当天的 8:30 时间
        var baseTime = new DateTime(actualTime.Year, actualTime.Month, actualTime.Day, 8, 30, 0);

        // 如果实际时间早于8:30，返回0
        if (actualTime <= baseTime)
            return 0;

        // 计算实际时间与8:30之间的时间差（以小时表示）
        var hoursDiff = (actualTime - baseTime).TotalHours;

        // 向下取 0.5 的倍数
        var rounded = Math.Floor(hoursDiff * 2) / 2.0;

        return rounded;
    }
}