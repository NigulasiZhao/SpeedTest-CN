using System.Text.Json;
using SpeedTest_CN.Models.PmisAndZentao;

namespace SpeedTest_CN.Common;

public class PmisHelper(IConfiguration configuration, ILogger<ZentaoHelper> logger, PushMessageHelper pushMessageHelper)
{
    /// <summary>
    /// 查询日报列表
    /// </summary>
    /// <returns></returns>
    public QueryMyByDateOutput QueryMyByDate()
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var postResponse = httpHelper.PostAsync(pmisInfo.Url + "/unioa/job/userWork/queryMy", new
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
        }, new Dictionary<string, string> { { "authorization", pmisInfo.Authorization } }).Result;
        var result = JsonSerializer.Deserialize<QueryMyByDateOutput>(postResponse.Content.ReadAsStringAsync().Result);
        return result;
    }

    /// <summary>
    /// 根据日期及用户ID获取每日工作计划明细
    /// </summary>
    /// <param name="fillDate"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public GetByDateAndUserIdResponse QueryWorkDetailByDate(string fillDate, string userId)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var getResponse = httpHelper.GetAsync(pmisInfo.Url + $"/unioa/job/userWork/getByDateAndUserId?fillDate={fillDate}&userId={userId}&type=0",
            new Dictionary<string, string> { { "authorization", pmisInfo.Authorization } }).Result;
        var result = JsonSerializer.Deserialize<GetByDateAndUserIdResponse>(getResponse.Content.ReadAsStringAsync().Result);
        return result;
    }

    /// <summary>
    /// 提交工作日报
    /// </summary>
    /// <param name="fillDate"></param>
    /// <param name="userId"></param>
    public PMISInsertResponse CommitWorkLogByDate(string fillDate, string userId)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var workLogBody = QueryWorkDetailByDate(fillDate, userId);
        workLogBody.Response.status = 1;
        foreach (var detailItem in workLogBody.Response.details)
        {
            detailItem.target = detailItem.description;
            detailItem.planFinishAct = detailItem.description;
            detailItem.responsibility = "负责基于.NET平台的后端代码开发与单元测试";
            detailItem.workType = "代码开发";
            detailItem.realJob = detailItem.description;
        }

        var postRespone = httpHelper.PostAsync(pmisInfo.Url + "/unioa/job/userWork/insert", workLogBody.Response, new Dictionary<string, string> { { "authorization", pmisInfo.Authorization } })
            .Result;
        var result = JsonSerializer.Deserialize<PMISInsertResponse>(postRespone.Content.ReadAsStringAsync().Result);
        if (result.Success) pushMessageHelper.Push("日报", $"{DateTime.Now:yyyy-MM-dd}已发送\n今日完成" + workLogBody.Response.details.Count + " 条任务", PushMessageHelper.PushIcon.Note);

        return result;
    }
}