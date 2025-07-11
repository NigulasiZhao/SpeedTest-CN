using System.Net.Http.Headers;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpeedTest_CN.Models.PmisAndZentao;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SpeedTest_CN.Common;

public class PmisHelper(IConfiguration configuration, ILogger<ZentaoHelper> logger, PushMessageHelper pushMessageHelper, TokenService tokenService)
{
    /// <summary>
    /// 查询日报列表
    /// </summary>
    /// <returns></returns>
    public JObject QueryMyByDate()
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
        }, new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } }).Result;
        var json = JObject.Parse(postResponse.Content.ReadAsStringAsync().Result);
        //var result = JsonSerializer.Deserialize<QueryMyByDateOutput>(postResponse.Content.ReadAsStringAsync().Result);
        return json;
    }

    /// <summary>
    /// 根据日期及用户ID获取每日工作计划明细
    /// </summary>
    /// <param name="fillDate"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public JObject QueryWorkDetailByDate(string fillDate, string userId)
    {
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var getResponse = httpHelper.GetAsync(pmisInfo.Url + $"/unioa/job/userWork/getByDateAndUserId?fillDate={fillDate}&userId={userId}&type=0",
            new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } }).Result;
        var json = JObject.Parse(getResponse.Content.ReadAsStringAsync().Result);
        //var result = JsonSerializer.Deserialize<GetByDateAndUserIdResponse>(getResponse.Content.ReadAsStringAsync().Result);
        return json;
    }

    /// <summary>
    /// 提交工作日报
    /// </summary>
    /// <param name="fillDate"></param>
    /// <param name="userId"></param>
    public PMISInsertResponse CommitWorkLogByDate(string fillDate, string userId)
    {
        var finishCount = 0;
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var httpHelper = new HttpRequestHelper();
        var workLogBody = QueryWorkDetailByDate(fillDate, userId);
        workLogBody["Response"]!["status"] = 1;
        if (workLogBody["Response"]?["details"] is JArray dataArray)
            foreach (var jToken in dataArray)
            {
                var item = (JObject)jToken;
                item["target"] = item["description"];
                item["planFinishAct"] = item["description"];
                item["responsibility"] = "负责基于.NET平台的后端代码开发与单元测试";
                item["workType"] = "代码开发";
                item["realJob"] = item["description"];
                finishCount++;
            }

        var res = workLogBody["Response"]?.ToString(Formatting.None);
        var postRespone = httpHelper.PostAsyncStringBody(pmisInfo?.Url + "/unioa/job/userWork/insert", workLogBody["Response"]?.ToString(Formatting.None),
                new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } })
            .Result;
        var result = JsonSerializer.Deserialize<PMISInsertResponse>(postRespone.Content.ReadAsStringAsync().Result);
        if (result.Success) pushMessageHelper.Push("日报", $"{DateTime.Now:yyyy-MM-dd}已发送\n今日完成" + finishCount + " 条任务", PushMessageHelper.PushIcon.Note);
        return result;
    }

    /// <summary>
    /// 通过项目编码获取PMIS项目信息
    /// </summary>
    /// <param name="projectCode"></param>
    /// <returns></returns>
    public ProjectInfo GetProjectInfo(string projectCode)
    {
        var projectInfo = new ProjectInfo();
        var httpHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var requestObject = new
        {
            url = pmisInfo.TenantDlmeasureUrl + "/hddev/form/formobjectdata/project_query:1/query.json",
            body = new
            {
                index = 1,
                size = -1,
                conditions = new[]
                {
                    new
                    {
                        Field = "contract_id",
                        Value = projectCode,
                        Operate = "like",
                        Relation = "or"
                    },
                    new
                    {
                        Field = "contract_unit",
                        Value = projectCode,
                        Operate = "like",
                        Relation = "or"
                    },
                    new
                    {
                        Field = "project_name",
                        Value = projectCode,
                        Operate = "like",
                        Relation = "or"
                    }
                },
                order = Array.Empty<object>(),
                authority = new
                {
                    tenantIds = (object?)null
                },
                conditionsSql = Array.Empty<object>()
            }
        };
        var postRespone = httpHelper.PostAsync(pmisInfo.TenantDlmeasureUrl + "/hddev/sys/sysinterface/externalInterface/post", requestObject,
                new Dictionary<string, string> { { "authorization", tokenService.GetTokenAsync() } })
            .Result;
        var projectJson = JObject.Parse(postRespone.Content.ReadAsStringAsync().Result);
        if (projectJson["Response"] != null)
            if (projectJson["Response"]?["rows"] is JArray dataArray)
            {
                var jToken = dataArray.First();
                projectInfo.contract_id = jToken["contract_id"]!.ToString();
                projectInfo.contract_unit = jToken["contract_unit"]!.ToString();
                projectInfo.project_name = jToken["project_name"]!.ToString();
            }

        return projectInfo;
    }

    /// <summary>
    /// 创建加班第一步
    /// </summary>
    /// <param name="projectInfo"></param>
    /// <param name="orderNo"></param>
    /// <returns></returns>
    public string OvertimeWork_Insert(ProjectInfo projectInfo, string orderNo, string Content)
    {
        var id = string.Empty;
        var httpHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        //var projectInfo = GetProjectInfo(projectCode);
        var requestObject = new Dictionary<string, object?>
        {
            { "child_groups", new object[] { } },
            { "user_sn", pmisInfo.UserAccount },
            { "pms_pushed", "0" },
            { "work_date", DateTime.Now.ToString("yyyy-MM-dd") },
            { "user_id$$text", pmisInfo.UserName },
            { "user_id", pmisInfo.UserId },
            { "org_id$$text", "管网产品组" },
            { "org_id", "67" },
            { "work_overtime_type", "1" },
            { "work_type", "1" },
            { "contract_id", projectInfo.contract_id },
            { "contract_unit", projectInfo.contract_unit },
            { "project_name", projectInfo.project_name },
            { "position", "1" },
            { "plan_start_time", DateTime.Now.ToString("yyyy-MM-dd") + " 17:30" },
            { "plan_end_time", DateTime.Now.ToString("yyyy-MM-dd") + " 19:30" },
            { "plan_work_overtime_hour", 2 },
            { "subject_matter", Content },
            { "reason", "1" },
            { "order_no", orderNo },
            { "remark", "" },
            { "work_overtime_type$$text", "延时加班" },
            { "work_type$$text", "项目开发/测试/设计" },
            { "position$$text", "公司" },
            { "reason$$text", "上线支撑" },
            { "product_name", null } // 明确声明为 null
        };
        var json = JsonConvert.SerializeObject(requestObject, Formatting.Indented);
        var postRespone = httpHelper.PostAsync(pmisInfo.TenantDlmeasureUrl + "/hddev/form/formobjectdata/oa_workovertime_plan_apply:7/insert.json", requestObject,
                new Dictionary<string, string> { { "token", tokenService.GetTokenAsync() }, { "uniwaterutoken", tokenService.GetTokenAsync() } })
            .Result;
        var projectJson = JObject.Parse(postRespone.Content.ReadAsStringAsync().Result);
        if (projectJson["Response"] == null) return id;
        if (projectJson["Response"]!["id"] != null)
            id = projectJson["Response"]!["id"]!.ToString();

        return id;
    }

    /// <summary>
    /// 创建加班第二步
    /// </summary>
    /// <param name="projectInfo"></param>
    /// <param name="id"></param>
    /// <param name="orderNo"></param>
    /// <returns></returns>
    public string OvertimeWork_CreateOrder(ProjectInfo projectInfo, string id, string orderNo, string Content)
    {
        var ProcessId = string.Empty;
        var httpHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var requestObject = new Dictionary<string, object?>
        {
            { "child_groups", new object[] { } },
            { "user_sn", pmisInfo.UserAccount },
            { "pms_pushed", "0" },
            { "work_date", DateTime.Now.ToString("yyyy-MM-dd") },
            { "user_id$$text", pmisInfo.UserName },
            { "user_id", pmisInfo.UserId },
            { "org_id$$text", "管网产品组" },
            { "org_id", "67" },
            { "work_overtime_type", "1" },
            { "work_type", "1" },
            { "contract_id", projectInfo.contract_id },
            { "contract_unit", projectInfo.contract_unit },
            { "project_name", projectInfo.project_name },
            { "position", "1" },
            { "plan_start_time", DateTime.Now.ToString("yyyy-MM-dd") + " 17:30" },
            { "plan_end_time", DateTime.Now.ToString("yyyy-MM-dd") + " 19:30" },
            { "plan_work_overtime_hour", 2 },
            { "subject_matter", Content },
            { "reason", "1" },
            { "order_no", orderNo },
            { "remark", "" },
            { "work_overtime_type$$text", "延时加班" },
            { "work_type$$text", "项目开发/测试/设计" },
            { "position$$text", "公司" },
            { "reason$$text", "上线支撑" },
            { "product_name", null }, // null 明确声明
            { "creator_gid", "67" },
            { "creator_gnm", "管网产品组" },
            { "creator_id", pmisInfo.UserId },
            { "creator_nm", pmisInfo.UserName },
            { "creator_duty", null }, // null 明确声明
            { "creator_mobile", pmisInfo.UserMobile },
            { "creator_sn", pmisInfo.UserAccount },
            { "id", id },
            { "", "" }, // 注意：空字段名称
            { "$$formHtmlId", "b187562cecea44598d9cdbe2bf5efc42" },
            { "$$saveType", "N" },
            { "$$saveFields", "" },
            { "$$objectPK", "id" }
        };

        var json = JsonConvert.SerializeObject(requestObject, Formatting.Indented);
        var postRespone = httpHelper.PostAsync(pmisInfo.TenantDlmeasureUrl + "/bpm/customize-api/jiaban_test/create-order2", requestObject,
                new Dictionary<string, string> { { "token", tokenService.GetTokenAsync() }, { "uniwaterutoken", tokenService.GetTokenAsync() } })
            .Result;
        var projectJson = JObject.Parse(postRespone.Content.ReadAsStringAsync().Result);
        if (projectJson["Response"] == null) return id;
        if (projectJson["Response"]!["id"] != null)
            ProcessId = projectJson["Response"]!["id"]!.ToString();

        return ProcessId;
    }

    public JObject OvertimeWork_Query(string id)
    {
        //var id = string.Empty;
        var httpHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var requestObject = new
        {
            conditions = new[]
            {
                new
                {
                    Field = "id",
                    Value = id,
                    Operate = "=",
                    Relation = "and"
                }
            },
            order = new object[] { },
            index = 1,
            size = 20000
        };

        var json = JsonConvert.SerializeObject(requestObject, Formatting.Indented);
        var postRespone = httpHelper.PostAsync(pmisInfo.TenantDlmeasureUrl + "/hddev/form/formobjectdata/oa_workovertime_plan_apply:7/query.json", requestObject,
                new Dictionary<string, string> { { "token", tokenService.GetTokenAsync() }, { "uniwaterutoken", tokenService.GetTokenAsync() } })
            .Result;
        var projectJson = JObject.Parse(postRespone.Content.ReadAsStringAsync().Result);
        return projectJson;
    }

    /// <summary>
    /// 创建加班第三步
    /// </summary>
    /// <param name="projectInfo"></param>
    /// <param name="id"></param>
    /// <param name="orderNo"></param>
    /// <param name="processId"></param>
    /// <returns></returns>
    public JObject OvertimeWork_Update(ProjectInfo projectInfo, string id, string orderNo, string processId, string Content)
    {
        //var id = string.Empty;
        var processInfo = OvertimeWork_Query(id);
        var httpHelper = new HttpRequestHelper();
        var pmisInfo = configuration.GetSection("PMISInfo").Get<PMISInfo>();
        var requestObject = new Dictionary<string, object?>
        {
            { "child_groups", new object[] { } },
            { "user_sn", pmisInfo.UserAccount },
            { "pms_pushed", "0" },
            { "work_date", DateTime.Now.ToString("yyyy-MM-dd") },
            { "user_id$$text", pmisInfo.UserName },
            { "user_id", pmisInfo.UserId },
            { "org_id$$text", "管网产品组" },
            { "org_id", "67" },
            { "work_overtime_type", "1" },
            { "work_type", "1" },
            { "contract_id", projectInfo.contract_id },
            { "contract_unit", projectInfo.contract_unit },
            { "project_name", projectInfo.project_name },
            { "position", "1" },
            { "plan_start_time", DateTime.Now.ToString("yyyy-MM-dd") + " 17:30" },
            { "plan_end_time", DateTime.Now.ToString("yyyy-MM-dd") + " 19:30" },
            { "plan_work_overtime_hour", 2 },
            { "subject_matter", Content },
            { "reason", "1" },
            { "order_no", orderNo },
            { "remark", "" },
            { "work_overtime_type$$text", "延时加班" },
            { "work_type$$text", "项目开发/测试/设计" },
            { "position$$text", "公司" },
            { "reason$$text", "上线支撑" },
            { "id", id },
            { "", "" },
            { "$$createProcessFlag", 1 },
            { "$$createProcessId", processId },
            { "hddev_proc_task", processInfo["hddev_proc_task"] },
            { "hddev_proc_status", processInfo["hddev_proc_status"] },
            { "hddev_proc_task_code", processInfo["hddev_proc_task_code"] },
            { "hddev_business_key", processInfo["hddev_business_key"] },
            { "product_name", null }
        };
        var json = JsonConvert.SerializeObject(requestObject, Formatting.Indented);
        var postRespone = httpHelper.PostAsync(pmisInfo.TenantDlmeasureUrl + "/hddev/form/formobjectdata/oa_workovertime_plan_apply:7/update.json", requestObject,
                new Dictionary<string, string> { { "token", tokenService.GetTokenAsync() }, { "uniwaterutoken", tokenService.GetTokenAsync() } })
            .Result;
        var projectJson = JObject.Parse(postRespone.Content.ReadAsStringAsync().Result);
        return projectJson;
    }
}