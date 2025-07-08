using Microsoft.AspNetCore.Mvc;
using SpeedTest_CN.Common;
using SpeedTest_CN.Models.PmisAndZentao;

namespace SpeedTest_CN.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class PmisAndZentaoController(
    IConfiguration configuration,
    ILogger<SpeedTestController> logger,
    ZentaoHelper zentaoHelper,
    AttendanceHelper attendanceHelper,
    PmisHelper pmisHelper,
    PushMessageHelper pushMessageHelper)
    : Controller
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
    [EndpointSummary("根据日期计算工时")]
    [HttpGet]
    public double GetWorkHoursByDate(DateTime date)
    {
        var result = attendanceHelper.GetWorkHoursByDate(date);
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("获取已上报列表")]
    [HttpGet]
    public QueryMyByDateOutput QueryMyByDate()
    {
        return pmisHelper.QueryMyByDate();
    }

    [Tags("PMIS")]
    [EndpointSummary("获取工作明细")]
    [HttpGet]
    public GetByDateAndUserIdResponse QueryByDateAndUserId(string fillDate = "2025-06-27")
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var result = pmisHelper.QueryWorkDetailByDate(fillDate, pmisInfo.UserId);
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("提交工作日志")]
    [HttpGet]
    public PMISInsertResponse CommitWorkLogByDate(string fillDate = "2025-06-27")
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var result = pmisHelper.CommitWorkLogByDate(fillDate, pmisInfo.UserId);
        return result;
    }

    [Tags("PMIS")]
    [EndpointSummary("测试推送")]
    [HttpGet]
    public string PushMessage()
    {
        pushMessageHelper.Push("禅道", "pushMessage", PushMessageHelper.PushIcon.Zentao);
        return "";
    }
}