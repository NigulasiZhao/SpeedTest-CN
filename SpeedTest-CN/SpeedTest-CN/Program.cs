using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SpeedTest_CN;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Configuration.AddJsonFile(AppDomain.CurrentDomain.BaseDirectory + "appsettings.json", optional: false, reloadOnChange: true);
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()  // ���������κ�Դ������
              .AllowAnyMethod()  // �����κ� HTTP ������GET��POST��PUT��DELETE �ȣ�
              .AllowAnyHeader(); // �����κ�����ͷ
    });
});
builder.Services.AddSingleton<IDbConnection>(sp =>
{
    return new NpgsqlConnection(builder.Configuration["Connection"]); // �滻Ϊ NpgsqlConnection ������������
});
builder.Services.AddHangfire(config =>
config.UsePostgreSqlStorage(c =>
        c.UseNpgsqlConnection(builder.Configuration["Connection"].ToString())));
builder.Services.AddHangfireServer();
builder.Services.AddSingleton<HangFireHelper>();
builder.Services.AddSingleton<DatabaseInitializer>();
var app = builder.Build();
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
