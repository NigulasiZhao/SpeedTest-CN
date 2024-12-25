using Dapper;
using Microsoft.AspNetCore.Mvc;
using SpeedTest_CN.Models;
using SpeedTest_CN.SpeedTest;
using System.Data;
using static SpeedTest_CN.SpeedTestHelper;

namespace SpeedTest_CN.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class SpeedTestController : Controller
    {
        private readonly IDbConnection _DbConnection;
        public SpeedTestController(IDbConnection DbConnection)
        {
            _DbConnection = DbConnection;
        }
        [HttpGet]
        public string Index()
        {
            SpeedTestHelper speedTestHelper = new SpeedTestHelper();
            Server speedResult = speedTestHelper.StartSpeedTest();
            return string.Format("下载速度: {0} Mbps;  上传速度: {1} Mbps", speedResult.downloadSpeed, speedResult.uploadSpeed); ;
        }
        [HttpGet]
        public ActionResult latest()
        {
            SpeedRecord speedRecord = _DbConnection.Query<SpeedRecord>("select * from speedrecord order by created_at desc").First();
            SpeedRecordResponse speedRecordResponse = new SpeedRecordResponse();
            speedRecordResponse.Message = "ok";
            speedRecordResponse.Data = speedRecord;
            // 返回结果
            return Json(speedRecordResponse);
        }
    }
}
