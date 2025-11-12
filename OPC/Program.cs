using OPC.Controllers;
using OPC;
using OPC.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 添加HttpClient工厂
builder.Services.AddHttpClient();

// 添加SignalR
builder.Services.AddSignalR();

// 👇 添加 OPC UA 服务
// 这是关键：注册 OPC 服务器为单例
builder.Services.AddSingleton<OpcUaServer>();

// 添加CORS策略
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// 添加健康检查端点
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

// 添加根路径重定向到Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

// 👇 OPC 服务生命周期管理（添加这段代码）
try
{
    var opcServer = app.Services.GetRequiredService<OpcUaServer>();
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

    // 应用启动完成时启动 OPC 服务器
    lifetime.ApplicationStarted.Register(async () =>
    {
        try
        {
            Console.WriteLine("");
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  正在启动 OPC UA 服务器...                                       ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            
            await opcServer.StartAsync();
            
            Console.WriteLine("");
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  🚀 OPC UA 服务器启动成功！                                      ║");
            Console.WriteLine("║                                                                ║");
            Console.WriteLine("║  📡 OPC UA 地址:    opc.tcp://localhost:4840                   ║");
            Console.WriteLine("║  🌐 REST API:      http://localhost:5001/api/opcdata/all      ║");
            Console.WriteLine("║  📊 Swagger:       http://localhost:5001/swagger               ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine("");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] OPC UA 服务器启动失败: {ex.Message}");
            Console.WriteLine($"详情: {ex.InnerException?.Message}");
        }
    });

    // 应用关闭时停止 OPC 服务器
    lifetime.ApplicationStopping.Register(async () =>
    {
        try
        {
            Console.WriteLine("");
            Console.WriteLine("[信息] 正在关闭 OPC UA 服务器...");
            await opcServer.StopAsync();
            Console.WriteLine("[信息] OPC UA 服务器已关闭");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] OPC UA 服务器关闭失败: {ex.Message}");
        }
    });
}
catch (Exception ex)
{
    Console.WriteLine($"[警告] OPC 服务配置异常: {ex.Message}");
}
// 👆 OPC 生命周期管理代码添加完成

app.Run();