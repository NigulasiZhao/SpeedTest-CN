using Dapper;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SpeedTest_CN.Models.Attendance;
using System.Data;
using System.Data.Common;
using Npgsql;
using LibGit2Sharp;
using System.IO;

namespace SpeedTest_CN
{
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
                string createTableSql = @"
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
                string createTableSql = @"
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
                string createTableSql = @"
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
                string createTableSql = @"
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
            if (_dbConnection.Query<int>("SELECT COUNT(0) FROM attendancerecord").First() == 0)
            {
                AttendanceRecordResult infoResult = new AttendanceRecordResult();
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", _Configuration["yinuotoken"]);
                DateTime StartDate = DateTime.Parse("2023-07-01");
                while (StartDate < DateTime.Now)
                {
                    var response = client.GetAsync("http://122.225.71.14:10001/hd-oa/api/oaUserClockInRecord/clockInDataMonth?yearMonth=" + StartDate.ToString("yyyy-MM")).Result;
                    string result = response.Content.ReadAsStringAsync().Result;
                    AttendanceResponse ResultModel = JsonConvert.DeserializeObject<AttendanceResponse>(result);
                    if (ResultModel.Code == 200)
                    {
                        _dbConnection.Execute($"INSERT INTO public.attendancerecord(attendancemonth,workdays,latedays,earlydays) VALUES('{StartDate.ToString("yyyy-MM")}',{ResultModel.Data.WorkDays},{ResultModel.Data.LateDays},{ResultModel.Data.EarlyDays});");
                        foreach (var item in ResultModel.Data.DayVoList)
                        {
                            DateTime flagedate = DateTime.Parse(StartDate.ToString("yyyy-MM") + "-" + item.Day);
                            if (item.WorkHours != null)
                            {
                                _dbConnection.Execute($@"INSERT INTO public.attendancerecordday(untilthisday,day,checkinrule,isnormal,isabnormal,isapply,clockinnumber,workhours,attendancedate)
                                                        VALUES({item.UntilThisDay},{item.Day},'{item.CheckInRule}','{item.IsNormal}','{item.IsAbnormal}','{item.IsApply}',{item.ClockInNumber},{item.WorkHours},to_timestamp('{flagedate.ToString("yyyy-MM-dd 00:00:00")}', 'yyyy-mm-dd hh24:mi:ss'));");
                                if (item.DetailList != null)
                                {
                                    foreach (var daydetail in item.DetailList)
                                    {
                                        _dbConnection.Execute($@"INSERT INTO public.attendancerecorddaydetail(id,recordid,clockintype,clockintime,attendancedate)
                                                        VALUES({daydetail.Id},{daydetail.RecordId},'{daydetail.ClockInType}',to_timestamp('{daydetail.ClockInTime}', 'yyyy-mm-dd hh24:mi:ss'),to_timestamp('{flagedate.ToString("yyyy-MM-dd 00:00:00")}', 'yyyy-mm-dd hh24:mi:ss'));");
                                    }
                                }
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
                string dataSql = "";
                string createTableSql = @"
                                   CREATE TABLE public.gogsrecord (
                                                	id varchar(100) NULL,
                                                	repositoryname varchar(200) NULL,
                                                	branchname varchar(200) NULL,
                                                	commitsdate timestamp NULL
                                                );
                                                ";
                string[] directories = Directory.GetDirectories(AppDomain.CurrentDomain.BaseDirectory + "ProjectGit");
                string targetEmail = _Configuration["GogsEmail"];
                var allCommits = new Dictionary<string, List<Commit>>();
                foreach (var repoPath in directories)
                {
                    string folderName = Path.GetFileName(repoPath);
                    if (Directory.Exists(repoPath + "/.git"))
                    {
                        using (var repo = new Repository(repoPath))
                        {
                            foreach (var branch in repo.Branches)
                            {
                                allCommits.Add(repoPath + branch.FriendlyName, branch.Commits.Where(commit => commit.Author.Email == targetEmail).ToList());
                            }
                        }
                    }
                }
                var uniqueCommits = new HashSet<Commit>(allCommits.SelectMany(kvp => kvp.Value));
                foreach (var commit in uniqueCommits)
                {
                    dataSql += @$"INSERT INTO public.gogsrecord(id,commitsdate) VALUES('{commit.Id}',to_timestamp('{commit.Committer.When.ToString("yyyy-MM-dd HH:MM:ss")}', 'yyyy-mm-dd hh24:mi:ss'));";
                }
                _dbConnection.Execute(createTableSql);
                _dbConnection.Execute(dataSql);
            }
            _dbConnection.Dispose();
        }
        private bool TableExists(string tableName, IDbConnection _dbConnection)
        {
            string checkTableSql = $@"select count(0) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{tableName}';";
            return _dbConnection.Query<int>(checkTableSql).First() == 0 ? true : false;
        }
    }
}
