using Dapper;
using LibGit2Sharp;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Npgsql;
using SpeedTest_CN.Models.Attendance;
using SpeedTest_CN.Models.Gogs;
using System.Data;
using System.Data.Common;
using System.Text;

namespace SpeedTest_CN.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class GogsController : Controller
    {
        private readonly IConfiguration _Configuration;
        public GogsController(IConfiguration configuration)
        {
            _Configuration = configuration;
        }
        [HttpGet]
        public ActionResult latest()
        {
            IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
            //string[] directories = Directory.GetDirectories(AppDomain.CurrentDomain.BaseDirectory + "/ProjectGit");
            //string targetEmail = "185653517@qq.com";
            //var allCommits = new Dictionary<string, List<Commit>>();
            //foreach (var repoPath in directories)
            //{
            //    if (Directory.Exists(repoPath + "/.git"))
            //    {
            //        using (var repo = new Repository(repoPath))
            //        {
            //            foreach (var branch in repo.Branches)
            //            {
            //                allCommits.Add(repoPath + branch.FriendlyName, branch.Commits.Where(commit => commit.Author.Email == targetEmail).ToList());
            //            }
            //        }
            //    }
            //}
            //var uniqueCommits = new HashSet<Commit>(allCommits.SelectMany(kvp => kvp.Value));
            //var commitDates = uniqueCommits
            //.Select(commit => commit.Author.When.Date) // 提取日期部分
            //.Distinct()                                // 去重日期
            //.ToList();
            //var userCommits = allCommits.Sum(e => e.Value.Count);
            int commitDates = _DbConnection.Query<int>(@"select
                                                          	count(0)
                                                          from
                                                          	(
                                                          	select
                                                          		to_char(commitsdate,
                                                          		'yyyy-mm-dd')
                                                          	from
                                                          		public.gogsrecord
                                                          	group by
                                                          		to_char(commitsdate,
                                                          		'yyyy-mm-dd') )").First();
            int uniqueCommits = _DbConnection.Query<int>(@"select
                                                            	count(0)
                                                            from
                                                            	public.gogsrecord").First();
            return Json(new
            {
                commitDates = commitDates,
                uniqueCommits = uniqueCommits,
                DayAvg = Math.Round((double)uniqueCommits / (double)commitDates, 2)
            });
        }
        [HttpGet]
        public ActionResult calendar(string start = "", string end = "")
        {
            IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
            string sqlwhere = " where 1=1 ";
            if (!string.IsNullOrEmpty(start))
            {
                sqlwhere += $" and a.commitsdate >= '{DateTime.Parse(start)}'";
            }
            if (!string.IsNullOrEmpty(end))
            {
                sqlwhere += $" and a.commitsdate <= '{DateTime.Parse(end).AddDays(1).AddSeconds(-1)}'";
            }
            List<GogsCalendar> WorkList = _DbConnection.Query<GogsCalendar>(@"select
                                                                                                        		a.id as rownum,
	'commit '||case when repositoryname is null then '' else '仓库:'||repositoryname end || case when branchname is null then '' else ';分支:'||SPLIT_PART(branchname, '/', 
                 LENGTH(branchname) - LENGTH(REPLACE(branchname, '/', '')) + 1) end as title,
	to_char(timezone('UTC',
	a.commitsdate at TIME zone 'Asia/Shanghai'),
	'yyyy-mm-ddThh24:mi:ssZ') as airDateUtc,
	true as hasFile,
	coalesce(message,'') as message
                                                                                                        from
                                                                                                        	public.gogsrecord a
                                                                                                    " + sqlwhere + " order by commitsdate asc").ToList();
            _DbConnection.Dispose();
            return Json(WorkList);
        }
        [HttpPost]
        public ActionResult GogsPush([FromBody] WebhookPayload input)
        {
            IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
            string BranchName = input.Ref.Split("/").Last();
            string dataSql = "";
            try
            {
                if (input.Commits != null)
                {
                    if (input.Commits.Count > 0)
                    {
                        List<WebhookCommit> WebhookCommitList = input.Commits.Where(e => e.Committer.Email == _Configuration["GogsEmail"]).ToList();
                        foreach (var item in WebhookCommitList)
                        {
                            int CommitExists = _DbConnection.Query<int>("select count(0) from public.gogsrecord where id = :id", new { id = item.Id }).First();
                            if (CommitExists == 0)
                            {
                                _DbConnection.Execute($@"INSERT INTO public.gogsrecord(id,repositoryname,branchname,commitsdate,message) VALUES(:id,:repositoryname,:branchname,to_timestamp('{item.Timestamp.ToString("yyyy-MM-dd HH:MM:ss")}', 'yyyy-mm-dd hh24:mi:ss'),:message);"
                                    , new { id = item.Id, repositoryname = input.Repository.Name, branchname = input.Ref, message = item.Message });
                            }
                        }
                        _DbConnection.Dispose();
                    }
                }
            }
            catch (IOException e)
            {
                _DbConnection.Dispose();
                return Json(e.Message);
            }
            return Json("成功");
        }
        [HttpPost]
        public ActionResult GitHubPush([FromBody] GitHubWebhookPayload input)
        {
            IDbConnection _DbConnection = new NpgsqlConnection(_Configuration["Connection"]);
            string BranchName = input.@ref.Split("/").Last();
            string dataSql = "";
            try
            {
                if (input.commits != null)
                {
                    if (input.commits.Count > 0)
                    {
                        List<Models.Gogs.Commit> WebhookCommitList = input.commits.Where(e => e.committer.Email == _Configuration["GogsEmail"]).ToList();
                        foreach (var item in WebhookCommitList)
                        {
                            int CommitExists = _DbConnection.Query<int>("select count(0) from public.gogsrecord where id = :id", new { id = item.id }).First();
                            if (CommitExists == 0)
                            {
                                _DbConnection.Execute(@$"INSERT INTO public.gogsrecord(id,repositoryname,branchname,commitsdate,message) VALUES(:id,:repositoryname,:branchname,to_timestamp('{item.timestamp.ToString("yyyy-MM-dd HH:MM:ss")}', 'yyyy-mm-dd hh24:mi:ss'),:message);"
                                    , new { id = item.id, repositoryname = input.repository.name, branchname = input.@ref, message = item.message });
                            }
                        }
                        if (!string.IsNullOrEmpty(dataSql))
                        {
                            _DbConnection.Execute(dataSql);
                        }
                        _DbConnection.Dispose();
                    }
                }
            }
            catch (IOException e)
            {
                _DbConnection.Dispose();
                return Json(e.Message);
            }
            return Json("成功");
        }
    }
}
