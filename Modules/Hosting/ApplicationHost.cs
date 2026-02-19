using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using sharwapi.Core.Modules.Configuration;
using sharwapi.Core.Modules.Hosting;
using sharwapi.Core.Modules.Logging;
using sharwapi.Core.Modules.Middleware;
using sharwapi.Core.Modules.PluginManagement;
using sharwapi.Core.Modules.Routing;
using sharwapi.Core.Modules.Services;
using sharwapi.Contracts.Core;

namespace sharwapi.Core.Modules.Hosting;

/// <summary>
/// 应用程序主机
/// 负责组装和启动整个应用程序
/// </summary>
public class ApplicationHost
{
    private readonly string[] _args;
    private IConfiguration _configuration = null!;
    private WebApplication _app = null!;
    private DateTime _startTime;
    private string _apiName = null!;
    private string _apiVersion = null!;

    /// <summary>
    /// 初始化应用程序主机
    /// </summary>
    /// <param name="args">命令行参数</param>
    public ApplicationHost(string[] args)
    {
        _args = args;
        _startTime = DateTime.UtcNow;
    }

    /// <summary>
    /// 构建应用程序
    /// </summary>
    /// <returns>应用程序主机实例</returns>
    public ApplicationHost Build()
    {
        // 1. 构建配置
        _configuration = AppConfiguration.Build().Build();

        // 2. 初始化日志
        Logger.Initialize(_configuration);
        Log.Information("Starting web host");

        // 3. 创建 WebApplicationBuilder
        var builder = WebApplication.CreateBuilder(_args);

        // 4. 配置主机
        builder.Host.UseSerilogLogging();
        builder.ConfigureHostOptions();

        // 5. 获取 API 信息
        _apiName = AppConfiguration.GetApiName(_configuration);
        _apiVersion = AppConfiguration.GetApiVersion(_configuration);

        // 6. 加载插件
        var pluginLogger = new SerilogLoggerFactory(Log.Logger).CreateLogger("PluginLoader");
        var pluginLoader = new PluginLoader(_configuration, pluginLogger);
        var plugins = pluginLoader.LoadPlugins();

        // 7. 检查依赖
        var dependencyChecker = new PluginDependencyChecker(pluginLogger);
        plugins = dependencyChecker.CheckDependencies(plugins);

        // 8. 注册服务
        // 将插件集合注入到 DI 容器（作为单例），插件实现可从容器中获取此集合
        builder.Services.AddSingleton(plugins);
        
        var serviceRegistrar = new PluginServiceRegistrar(builder.Services, pluginLogger);
        serviceRegistrar.AddSwaggerServices(_apiName, _apiVersion);
        serviceRegistrar.RegisterPluginServices(plugins, _configuration);

        // 9. 构建应用
        _app = builder.Build();

        // 10. 配置中间件
        ExceptionHandling.Configure(_app);
        MiddlewarePipeline.Configure(_app, plugins);

        // 在开发环境中启用 Swagger UI
        if (_app.Environment.IsDevelopment())
        {
            _app.Logger.LogInformation("Enabling Swagger UI (Development Mode)...");
            _app.UseSwagger();
            _app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", $"{_apiName} {_apiVersion}");
                options.RoutePrefix = "swagger";
            });
        }

        // 11. 注册路由
        EndpointRegistration.RegisterPluginRoutes(_app, plugins, _configuration);
        EndpointRegistration.RegisterRootEndpoint(_app, _apiName, _apiVersion, _startTime);

        return this;
    }

    /// <summary>
    /// 运行应用程序
    /// </summary>
    public void Run()
    {
        try
        {
            _app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
