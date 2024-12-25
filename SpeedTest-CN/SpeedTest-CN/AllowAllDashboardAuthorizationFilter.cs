using Hangfire.Dashboard;

namespace SpeedTest_CN
{
    public class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            return true; // 允许所有人访问
        }
    }
}
