using Dapper;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SpeedTest_CN.Models.Attendance;
using System.Data;
using System.Data.Common;
using Npgsql;
using LibGit2Sharp;
using System.IO;

namespace SpeedTest_CN;

public class DatabaseInitializer
{
    private readonly IConfiguration _Configuration;

    public DatabaseInitializer(IConfiguration configuration)
    {
        _Configuration = configuration;
    }

    public void Initialize()
    {
        IDbConnection _dbConnection = new NpgsqlConnection(_Configuration["Connection"]);
        if (TableExists("speedrecord", _dbConnection))
        {
            var createTableSql = @"
                                    CREATE TABLE public.speedrecord (
                                    	id varchar(36) NOT NULL,
                                    	ping varchar(200) NULL,
                                    	download numeric(11, 2) NULL,
                                    	upload numeric(11, 2) NULL,
                                    	server_id float8 NULL,
                                    	server_host varchar(500) NULL,
                                    	server_name varchar(500) NULL,
                                    	url varchar(500) NULL,
                                    	scheduled float8 DEFAULT 0 NULL,
                                    	failed float8 DEFAULT 0 NULL,
                                    	created_at timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                                    	updated_at timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                                    	CONSTRAINT speedrecord_pk PRIMARY KEY (id)
                                    );";
            _dbConnection.Execute(createTableSql);
        }

        if (TableExists("attendancerecord", _dbConnection))
        {
            var createTableSql = @"
                                    CREATE TABLE public.attendancerecord (
                                                	attendancemonth varchar(100) NULL,
                                                	workdays float8 NULL,
                                                	latedays float8 NULL,
                                                	earlydays float8 NULL
                                                );
                                    COMMENT ON COLUMN public.attendancerecord.attendancemonth IS '考勤年月';
                                    COMMENT ON COLUMN public.attendancerecord.workdays IS '工作天数';
                                    COMMENT ON COLUMN public.attendancerecord.latedays IS '迟到天数';
                                    COMMENT ON COLUMN public.attendancerecord.earlydays IS '早退天数';";
            _dbConnection.Execute(createTableSql);
        }

        if (TableExists("attendancerecordday", _dbConnection))
        {
            var createTableSql = @"
                                    CREATE TABLE public.attendancerecordday (
                                                	untilthisday boolean NULL,
                                                	""day"" float8 NULL,
                                                	checkinrule varchar(100) NULL,
                                                	isnormal varchar(100) NULL,
                                                	isabnormal varchar(100) NULL,
                                                	isapply varchar(100) NULL,
                                                	clockinnumber float8 NULL,
                                                	workhours numeric(11, 2) NULL,
                                                	attendancedate timestamp NULL
                                                );
                                                ";
            _dbConnection.Execute(createTableSql);
        }

        if (TableExists("attendancerecorddaydetail", _dbConnection))
        {
            var createTableSql = @"
                                   CREATE TABLE public.attendancerecorddaydetail (
                                                	id float8 NULL,
                                                	recordid float8 NULL,
                                                	clockintype varchar(100) NULL,
                                                	clockintime timestamp NULL,
                                                	attendancedate timestamp NULL
                                                );
                                                ";
            _dbConnection.Execute(createTableSql);
        }

        if (TableExists("eventinfo", _dbConnection))
        {
            var createTableSql = @"
                                   CREATE TABLE public.eventinfo (
													id varchar(100) NULL,
													title varchar(200) NULL,
													message varchar(2000) NULL,
													clockintime timestamp NULL,
													color varchar(200) NULL,
													""source"" varchar(200) NULL,
													distinguishingmark varchar(200) NULL
												);
                                                ";
            _dbConnection.Execute(createTableSql);
        }

        if (TableExists("zentaotask", _dbConnection))
        {
            var createTableSql = @"
                                   CREATE TABLE public.zentaotask (
																	id integer NOT NULL,
																	project integer NULL,
																	execution integer NULL,
																	taskname varchar(1000) NULL,
																	estimate float8 NULL,
																	timeleft float8 NULL,
																	consumed float8 NULL,
																	registerhours float8 NULL,
																	taskstatus varchar(200) NULL,
																	eststarted timestamp NULL,
																	deadline timestamp NULL,
																	taskdesc varchar(1000) NULL,
																	openedby varchar(200) NULL,
																	openeddate timestamp NULL,
																	qiwangriqi timestamp NULL,
																	executionname varchar(500) NULL,
																	projectname varchar(500) NULL,
																	CONSTRAINT zentaotask_pk PRIMARY KEY (id)
																);
																
																-- Column comments
																
																COMMENT ON COLUMN public.zentaotask.id IS '任务id';
																COMMENT ON COLUMN public.zentaotask.project IS '项目id';
																COMMENT ON COLUMN public.zentaotask.execution IS '执行人id';
																COMMENT ON COLUMN public.zentaotask.taskname IS '任务名称';
																COMMENT ON COLUMN public.zentaotask.estimate IS '预估工时';
																COMMENT ON COLUMN public.zentaotask.timeleft IS '剩余工时';
																COMMENT ON COLUMN public.zentaotask.consumed IS '消耗工时';
																COMMENT ON COLUMN public.zentaotask.consumed IS '本人登记工时';
																COMMENT ON COLUMN public.zentaotask.taskstatus IS '任务状态';
																COMMENT ON COLUMN public.zentaotask.eststarted IS '开始日期';
																COMMENT ON COLUMN public.zentaotask.deadline IS '截止日期';
																COMMENT ON COLUMN public.zentaotask.taskdesc IS '任务说明';
																COMMENT ON COLUMN public.zentaotask.openedby IS '派单人';
																COMMENT ON COLUMN public.zentaotask.openeddate IS '派单日期';
																COMMENT ON COLUMN public.zentaotask.qiwangriqi IS '期望日期';
																COMMENT ON COLUMN public.zentaotask.executionname IS '项目名称';
																COMMENT ON COLUMN public.zentaotask.projectname IS '项目名称带编号';";
            _dbConnection.Execute(createTableSql);
        }

        if (_dbConnection.Query<int>("SELECT COUNT(0) FROM attendancerecord").First() == 0)
        {
            var infoResult = new AttendanceRecordResult();
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", _Configuration["yinuotoken"]);
            var StartDate = DateTime.Parse("2023-07-01");
            while (StartDate < DateTime.Now)
            {
                var response = client.GetAsync("http://122.225.71.14:10001/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" + StartDate.ToString("yyyy-MM")).Result;
                var result = response.Content.ReadAsStringAsync().Result;
                var ResultModel = JsonConvert.DeserializeObject<AttendanceResponse>(result);
                if (ResultModel.Code == 200)
                {
                    _dbConnection.Execute(
                        $"INSERT INTO public.attendancerecord(attendancemonth,workdays,latedays,earlydays) VALUES('{StartDate.ToString("yyyy-MM")}',{ResultModel.Data.WorkDays},{ResultModel.Data.LateDays},{ResultModel.Data.EarlyDays});");
                    foreach (var item in ResultModel.Data.DayVoList)
                    {
                        var flagedate = DateTime.Parse(StartDate.ToString("yyyy-MM") + "-" + item.Day);
                        if (item.WorkHours != null)
                        {
                            _dbConnection.Execute($@"INSERT INTO public.attendancerecordday(untilthisday,day,checkinrule,isnormal,isabnormal,isapply,clockinnumber,workhours,attendancedate)
                                                        VALUES({item.UntilThisDay},{item.Day},'{item.CheckInRule}','{item.IsNormal}','{item.IsAbnormal}','{item.IsApply}',{item.ClockInNumber},{item.WorkHours},to_timestamp('{flagedate.ToString("yyyy-MM-dd 00:00:00")}', 'yyyy-mm-dd hh24:mi:ss'));");
                            if (item.DetailList != null)
                                foreach (var daydetail in item.DetailList)
                                    _dbConnection.Execute($@"INSERT INTO public.attendancerecorddaydetail(id,recordid,clockintype,clockintime,attendancedate)
                                                        VALUES({daydetail.Id},{daydetail.RecordId},'{daydetail.ClockInType}',to_timestamp('{daydetail.ClockInTime}', 'yyyy-mm-dd hh24:mi:ss'),to_timestamp('{flagedate.ToString("yyyy-MM-dd 00:00:00")}', 'yyyy-mm-dd hh24:mi:ss'));");
                        }
                    }
                }

                //infoResult.WorkDays += ResultModel.Data.WorkDays;
                //infoResult.LateDays += ResultModel.Data.LateDays;
                //infoResult.EarlyDays += ResultModel.Data.EarlyDays;
                //infoResult.DayAvg += (double)ResultModel.Data.DayVoList.Where(e => e.WorkHours != null).Sum(e => e.WorkHours);
                StartDate = StartDate.AddMonths(1);
            }
        }

        if (TableExists("gogsrecord", _dbConnection))
        {
            var dataSql = "";
            var createTableSql = @"
                                   CREATE TABLE public.gogsrecord (
                                                	id varchar(100) NULL,
                                                	repositoryname varchar(200) NULL,
                                                	branchname varchar(200) NULL,
                                                	commitsdate timestamp NULL
                                                );
                                                ";
            _dbConnection.Execute(createTableSql);
            var directories = Directory.GetDirectories(AppDomain.CurrentDomain.BaseDirectory + "ProjectGit");
            if (directories.Length > 0)
            {
                var targetEmail = _Configuration["GogsEmail"];
                var allCommits = new Dictionary<string, List<Commit>>();
                foreach (var repoPath in directories)
                {
                    var folderName = Path.GetFileName(repoPath);
                    if (Directory.Exists(repoPath + "/.git"))
                        using (var repo = new Repository(repoPath))
                        {
                            foreach (var branch in repo.Branches) allCommits.Add(repoPath + branch.FriendlyName, branch.Commits.Where(commit => commit.Author.Email == targetEmail).ToList());
                        }
                }

                var uniqueCommits = new HashSet<Commit>(allCommits.SelectMany(kvp => kvp.Value));
                foreach (var commit in uniqueCommits)
                    dataSql +=
                        @$"INSERT INTO public.gogsrecord(id,commitsdate) VALUES('{commit.Id}',to_timestamp('{commit.Committer.When.ToString("yyyy-MM-dd HH:MM:ss")}', 'yyyy-mm-dd hh24:mi:ss'));";
                if (!string.IsNullOrEmpty(dataSql)) _dbConnection.Execute(dataSql);
            }
        }

        _dbConnection.Dispose();
    }

    private bool TableExists(string tableName, IDbConnection _dbConnection)
    {
        var checkTableSql = $@"select count(0) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{tableName}';";
        return _dbConnection.Query<int>(checkTableSql).First() == 0 ? true : false;
    }
}