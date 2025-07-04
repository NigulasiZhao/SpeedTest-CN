using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Dapper;
using Npgsql;
using SpeedTest_CN.Models.PmisAndZentao;

namespace SpeedTest_CN.Common;

public class ZentaoHelper(IConfiguration configuration, ILogger<ZentaoHelper> logger)
{
    /// <summary>
    /// 获取禅道token
    /// </summary>
    /// <returns></returns>
    public string GetZentaoToken()
    {
        var zentaoInfo = configuration.GetSection("ZentaoInfo").Get<ZentaoInfo>();
        var httpHelper = new HttpRequestHelper();
        var postResponse = httpHelper.PostAsync(zentaoInfo.Url + "/api.php/v1/tokens", new
        {
            account = zentaoInfo.Account,
            password = zentaoInfo.Password
        }).Result;
        var json = postResponse.Content.ReadAsStringAsync().Result;
        var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("token").GetString();
        return token;
    }

    /// <summary>
    /// 获取禅道任务
    /// </summary>
    /// <returns></returns>
    public List<ZentaoTaskItem> GetZentaoTask()
    {
        var zentaoInfo = configuration.GetSection("ZentaoInfo").Get<ZentaoInfo>();
        var zentaoToken = GetZentaoToken();
        var httpHelper = new HttpRequestHelper();
        var getTaskResponse = httpHelper.GetAsync(zentaoInfo.Url + "/my-work-task.json", new Dictionary<string, string> { { "Token", zentaoToken } }).Result;
        var outer = JsonSerializer.Deserialize<ZentaoResponse>(getTaskResponse.Content.ReadAsStringAsync().Result);
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All), // 确保不重新编码
            WriteIndented = true
        };
        var jsonDoc = JsonDocument.Parse(outer.data); // 内层是个 JSON 字符串
        var prettyJson = JsonSerializer.Serialize(jsonDoc.RootElement, options);
        var zentaoTaskResult = JsonSerializer.Deserialize<ZentaoTaskResponse>(prettyJson);
        return zentaoTaskResult.tasks;
    }

    /// <summary>
    /// 同步禅道任务
    /// </summary>
    /// <returns></returns>
    public bool SynchronizationZentaoTask()
    {
        try
        {
            var TaskList = GetZentaoTask();
            var sql = string.Empty;
            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            sql = TaskList.Aggregate(sql, (current, zentaoTaskItem) => current + $@"insert
        	into
        	public.zentaotask
        (id,
        	project,
        	execution,
        	taskname,
        	estimate,
        	timeleft,
            eststarted,
            consumed,
            taskstatus,
        	deadline,
        	taskdesc,
        	openedby,
        	openeddate,
        	qiwangriqi,
        	executionname,
        	projectname,registerhours)
        values({zentaoTaskItem.id},
        {zentaoTaskItem.project},
        {zentaoTaskItem.execution},
        '{zentaoTaskItem.name}',
        {zentaoTaskItem.estimate},
        {zentaoTaskItem.left},
        '{zentaoTaskItem.estStarted}',
         {zentaoTaskItem.consumed},
               '{zentaoTaskItem.status}',
        '{zentaoTaskItem.deadline}',
        '{zentaoTaskItem.desc}',
        '{zentaoTaskItem.openedBy}',
        '{zentaoTaskItem.openedDate}',
        '{zentaoTaskItem.qiwangriqi}',
        '{zentaoTaskItem.executionName}',
        '{zentaoTaskItem.projectName}',0) ON CONFLICT (id) DO NOTHING;");
            dbConnection.Execute(sql);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError("同步禅道任务异常:" + e.Message);
            return false;
        }
    }

    /// <summary>
    /// 完成禅道任务
    /// </summary>
    public void FinishZentaoTask(DateTime finishedDate, double totalHours)
    {
        try
        {
            IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
            var zentaoToken = GetZentaoToken();
            var zentaoInfo = configuration.GetSection("ZentaoInfo").Get<ZentaoInfo>();
            var httpHelper = new HttpRequestHelper();
            var tasklist = AllocateWork(finishedDate, totalHours);
            foreach (var task in tasklist)
            {
                var getResponse = httpHelper.PostAsync(zentaoInfo.Url + $"/api.php/v1/tasks/{task.Id}/finish", new
                {
                    currentConsumed = task.TimeConsuming,
                    assignedTo = "",
                    realStarted = task.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    finishedDate = task.EndTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    comment = "任务完成"
                }, new Dictionary<string, string> { { "Token", zentaoToken } }).Result;
                var outer = JsonSerializer.Deserialize<FinishZentaoTaskResponse>(getResponse.Content.ReadAsStringAsync().Result);
                dbConnection.Execute(
                    $@"UPDATE public.zentaotask SET consumed =consumed+ {outer.consumed},registerhours = registerhours + {task.TimeConsuming},taskstatus = '{outer.status}' WHERE ID = {outer.id}");
            }
        }
        catch (Exception e)
        {
            logger.LogError("同步禅道任务异常:" + e.Message);
        }
    }

    public List<TaskItem> AllocateWork(DateTime startDate, double totalHours)
    {
        var result = new List<TaskItem>();
        IDbConnection dbConnection = new NpgsqlConnection(configuration["Connection"]);
        var registerhours = dbConnection.Query<float>($@"select sum(registerhours) from public.zentaotask where eststarted = '{startDate:yyyy-MM-dd}'").First();
        totalHours -= registerhours;
        var tasks = dbConnection.Query<TaskItem>($@"select id,estimate from public.zentaotask where (taskstatus ='wait' or taskstatus = 'doing') and eststarted = '{startDate:yyyy-MM-dd}'").ToList();
        var current = new DateTime(startDate.Year, startDate.Month, startDate.Day, 8, 30, 0);

        foreach (var task in tasks)
        {
            var taskCopy = new TaskItem
            {
                Id = task.Id,
                Estimate = task.Estimate,
                StartTime = current
            };

            double timeAllocated = 0;

            while (timeAllocated < task.Estimate && totalHours >= 0.5)
            {
                // 跳过中午12:00~13:00
                if (current.Hour == 12 && current.Minute == 0)
                {
                    current = current.AddHours(1);
                    continue;
                }

                timeAllocated += 0.5;
                totalHours -= 0.5;
                current = current.AddMinutes(30);

                if (totalHours < 0.01) break;
            }

            taskCopy.TimeConsuming = timeAllocated;
            taskCopy.EndTime = current;
            result.Add(taskCopy);

            if (totalHours < 0.01) break;
        }

        return result;
    }
}