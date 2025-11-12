using OPC.Services;

var builder = WebApplication.CreateBuilder(args);

// ==================== 服务注册 ====================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();

// 注册 OPC UA 服务器为单例
builder.Services.AddSingleton<OpcUaServer>();

// 添加 CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", corsBuilder =>
    {
        corsBuilder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
    });
});

var app = builder.Build();

// ==================== 中间件配置 ====================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthorization();

// ==================== 路由配置 ====================
app.MapControllers();

// 健康检查端点
app.MapGet("/health", () =>
    Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

// 根路径重定向
app.MapGet("/", () => Results.Redirect("/swagger"))
    .WithName("Root");

// ==================== OPC UA 服务器生命周期管理 ====================
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var opcServer = app.Services.GetRequiredService<OpcUaServer>();

// 应用启动完成时启动 OPC 服务器
lifetime.ApplicationStarted.Register(async () =>
{
    try
    {
        await opcServer.StartAsync();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[致命错误] OPC UA 服务器启动失败: {ex.Message}");
        Console.ResetColor();
    }
});

// 应用关闭时停止 OPC 服务器
lifetime.ApplicationStopping.Register(async () =>
{
    try
    {
        await opcServer.StopAsync();
        opcServer.Dispose();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[错误] OPC UA 服务器关闭失败: {ex.Message}");
        Console.ResetColor();
    }
});

// ==================== 启动应用 ====================
app.Run();