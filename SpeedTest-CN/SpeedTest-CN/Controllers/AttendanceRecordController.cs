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
    }
}
