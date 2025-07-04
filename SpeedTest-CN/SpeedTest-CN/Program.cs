using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Localization;
using SpeedTest_CN;
using System.Globalization;
using Scalar.AspNetCore;
using Serilog;
using SpeedTest_CN.Common;

var builder = WebApplication.CreateBuilder(args);
if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "Logs")) Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "Logs");
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(AppDomain.CurrentDomain.BaseDirectory + "Logs/log.txt", rollingInterval: RollingInterval.Day) // 每天一个文件
    .CreateLogger();

builder.Host.UseSerilog();
// Add services to the container.
builder.Configuration.AddJsonFile(AppDomain.CurrentDomain.BaseDirectory + "appsettings.json", false, true);
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin() // 允许来自任何源的请求
            .AllowAnyMethod() // 允许任何 HTTP 方法（GET、POST、PUT、DELETE 等）
            .AllowAnyHeader(); // 允许任何请求头
    });
});
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(c =>
        c.UseNpgsqlConnection(builder.Configuration["Connection"].ToString())));
builder.Services.AddHangfireServer();
builder.Services.AddSingleton<HangFireHelper>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<ZentaoHelper>();
builder.Services.AddSingleton<AttendanceHelper>();
builder.Services.AddSingleton<PmisHelper>();
builder.Services.AddSingleton<PushMessageHelper>();
var app = builder.Build();
var zh = new CultureInfo("zh-CN");
zh.DateTimeFormat.FullDateTimePattern = "yyyy-MM-dd HH:mm:ss";
zh.DateTimeFormat.LongDatePattern = "yyyy-MM-dd";
zh.DateTimeFormat.LongTimePattern = "HH:mm:ss";
zh.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
zh.DateTimeFormat.ShortTimePattern = "HH:mm:ss";
IList<CultureInfo> supportedCultures = new List<CultureInfo>
{
    zh
};
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("zh-CN"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    initializer.Initialize();
}

app.UseCors("AllowAll");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllDashboardAuthorizationFilter() }
});
app.Services.GetRequiredService<HangFireHelper>().StartHangFireTask();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();