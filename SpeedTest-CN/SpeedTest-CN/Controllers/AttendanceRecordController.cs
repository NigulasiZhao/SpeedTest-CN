using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SpeedTest_CN.Models;
using SpeedTest_CN.Models.Attendance;
using System.Data;
using Dapper;
using Npgsql;

namespace SpeedTest_CN.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class AttendanceRecordController : Controller
    {
        private readonly IConfiguration _Configuration;
        public AttendanceRecordController(IConfiguration configuration)
        {
            _Configuration = configuration;
        }
        [HttpGet]
        public ActionResult latest()
        {
            IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
            int WorkDays = _DbConnection.Query<int>("select count(0) from (select to_char(attendancedate,'yyyy-mm-dd'),count(0) from public.attendancerecorddaydetail  group by to_char(attendancedate,'yyyy-mm-dd'))").First();
            decimal WorkHours = _DbConnection.Query<decimal>("select sum(workhours) from public.attendancerecordday").First();
            _DbConnection.Dispose();
            return Json(new
            {
                WorkDays = WorkDays,
                WorkHours = WorkHours,
                DayAvg = Math.Round((double)WorkHours / WorkDays, 2)
            });
        }
        [HttpGet]
        public ActionResult calendar(string start = "", string end = "")
        {
            IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
            string sqlwhere = " where 1=1 ";
            if (!string.IsNullOrEmpty(start))
            {
                sqlwhere += $" and a.clockintime >= '{DateTime.Parse(start)}'";
            }
            if (!string.IsNullOrEmpty(end))
            {
                sqlwhere += $" and a.clockintime <= '{DateTime.Parse(end).AddDays(1).AddSeconds(-1)}'";
            }
            List<AttendanceCalendarOutput> WorkList = _DbConnection.Query<AttendanceCalendarOutput>(@"select
                                                                                                    	a.id as rownum,
                                                                                                    	case
                                                                                                    		a.clockintype when '0' then '上班'
                                                                                                    		else '下班'
                                                                                                    	end as title,
                                                                                                    	to_char(timezone('UTC',
                                                                                                    	a.clockintime at TIME zone 'Asia/Shanghai'),
                                                                                                    	'yyyy-mm-ddThh24:mi:ssZ') as airDateUtc,
                                                                                                    	true as hasFile,
                                                                                                    	case
                                                                                                    		when b.workhours = 0 then 
                                                                                                    		'当日工时: ' || RTRIM(RTRIM(cast(ROUND(extract(EPOCH
                                                                                                    	from
                                                                                                    		(now() at TIME zone 'Asia/Shanghai' - a.clockintime))/ 3600,
                                                                                                    		1) as VARCHAR),
                                                                                                    		'0'),
                                                                                                    		'.')|| ' 小时'
                                                                                                    		else
                                                                                                    	'当日工时: ' || RTRIM(RTRIM(cast(b.workhours as VARCHAR),
                                                                                                    		'0'),
                                                                                                    		'.') || ' 小时'
                                                                                                    	end as workhours
                                                                                                    from
                                                                                                    	public.attendancerecorddaydetail a
                                                                                                    left join attendancerecordday b on
                                                                                                    	to_char(a.attendancedate,
                                                                                                    	'yyyy-mm-dd') = to_char(b.attendancedate,
                                                                                                    	'yyyy-mm-dd')
                                                                                                    " + sqlwhere + " order by clockintime").ToList();
            _DbConnection.Dispose();
            return Json(WorkList);
        }
    }
}
