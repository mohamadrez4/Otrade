using Hangfire.Dashboard;

public class HangfireCookieAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();

        return http.Request.Cookies.TryGetValue("HangfireAuth", out var value)
               && value == "true";
    }
}