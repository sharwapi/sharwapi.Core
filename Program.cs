using Microsoft.AspNetCore.Diagnostics;
using sharwapi.Contracts.Core;
using sharwapi.Core;
using System.Reflection;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Extensions.Logging;
using NuGet.Versioning;

// 用于记录服务启动时的运行时长（uptime）
var startTime = DateTime.UtcNow;

// 构建配置以初始化 Serilog
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// 初始化全局 Logger
sharwapi.Core.Logger.Initialize(configuration);
Log.Information("Starting web host");

// 创建 WebApplicationBuilder（应用与服务配置入口）
var builder = WebApplication.CreateBuilder(args);

// 将 Serilog 挂载到 Host，接管系统日志
builder.Host.UseSerilog();

builder.Services.Configure<HostOptions>(opts =>
{
    opts.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

// 创建用于插件加载器的 Logger (使用 SerilogLoggerFactory 桥接)
var pluginLoaderLogger = new SerilogLoggerFactory(Log.Logger).CreateLogger("PluginLoader");

// 从配置中读取 API 信息（名称与版本），若未配置则使用默认值
var apiName = builder.Configuration.GetValue<string>("ApiInfo:Name") ?? "CoreAPI";
var apiVersion = builder.Configuration.GetValue<string>("ApiInfo:Version") ?? "0.0.0";

// 加载位于运行目录下 Plugins 子目录的插件（DLL）
var plugins = LoadPlugins(builder.Configuration, pluginLoaderLogger);

// --- 依赖检查逻辑 ---
pluginLoaderLogger.LogInformation("Checking plugin dependencies...");
var loadedPluginsMap = plugins.ToDictionary(p => p.Name, p => p.Version);
var validPlugins = new List<IApiPlugin>();
foreach (var plugin in plugins)
{
    bool dependenciesMet = true;
    foreach (var dependency in plugin.Dependencies)
    {
        string depName = dependency.Key;
        string depRangeStr = dependency.Value ?? string.Empty;

        // 检查依赖插件是否存在
        if (!loadedPluginsMap.TryGetValue(depName, out var loadedVersionStr))
        {
            pluginLoaderLogger.LogError("Plugin '{PluginName}' failed to load. Missing dependency: '{DepName}'.", plugin.Name, depName);
            dependenciesMet = false;
            break;
        }

        // 解析当前加载的插件版本
        if (!NuGetVersion.TryParse(loadedVersionStr, out var loadedVersion))
        {
            pluginLoaderLogger.LogError("Plugin '{PluginName}' depends on '{DepName}', but the loaded version '{LoadedVer}' of dependency '{DepName}' has an invalid format.", 
                plugin.Name, depName, loadedVersionStr, depName);
            dependenciesMet = false;
            break;
        }

        // 解析依赖要求的版本范围
        // VersionRange.Parse 支持 "[1.0, 2.0)", "1.0" (即 >=1.0) 等标准写法
        bool isRangeValid = VersionRange.TryParse(depRangeStr, out var requiredRange);
        
        // 如果解析失败，尝试处理浮动版本 (例如 "1.*")
        if (!isRangeValid && FloatRange.TryParse(depRangeStr, out var floatRange))
        {
            // 检查浮动版本范围
            if (!floatRange.Satisfies(loadedVersion))
            {
                pluginLoaderLogger.LogError("Plugin '{PluginName}' requires '{DepName}' version '{DepRange}' (Floating), but loaded version '{LoadedVer}' is incompatible.", 
                    plugin.Name, depName, depRangeStr, loadedVersionStr);
                dependenciesMet = false;
                break;
            }
        }
        else if (isRangeValid)
        {
             // 检查标准版本范围
             if (!requiredRange.Satisfies(loadedVersion))
             {
                 pluginLoaderLogger.LogError("Plugin '{PluginName}' requires '{DepName}' version '{DepRange}', but loaded version '{LoadedVer}' is incompatible.", 
                     plugin.Name, depName, depRangeStr, loadedVersionStr);
                 dependenciesMet = false;
                 break;
             }
        }
        else
        {
             // 无法解析的版本要求
             pluginLoaderLogger.LogError("Plugin '{PluginName}' has an invalid dependency version format for '{DepName}': '{DepRange}'.", 
                 plugin.Name, depName, depRangeStr);
             dependenciesMet = false;
             dependenciesMet = false;
            break;
        }
    }

    if (dependenciesMet)
    {
        validPlugins.Add(plugin);
    }
}

// 移除未能满足依赖的插件
if (validPlugins.Count < plugins.Count)
{
    var removedCount = plugins.Count - validPlugins.Count;
    pluginLoaderLogger.LogWarning("{Count} plugins were unloaded due to missing or incompatible dependencies.", removedCount);
    plugins = validPlugins;
}
// --------------------

// 将插件集合注入到 DI 容器（作为单例），插件实现可从容器中获取此集合
builder.Services.AddSingleton(plugins);

// 让每个插件向 DI 容器注册它们自己的服务
pluginLoaderLogger.LogInformation("Registering plugin services...");
foreach (var plugin in plugins)
{
    // 实现配置隔离：为每个插件加载独立的配置文件
    // 路径格式：config/{PluginName}.json
    var configPath = Path.Combine(AppContext.BaseDirectory, "config", $"{plugin.Name}.json");
    
    // 检查当前插件的配置文件是否存在于指定路径
    if (!File.Exists(configPath))
    {
        // 尝试从插件实例中获取默认配置对象
        var defaultConfig = plugin.DefaultConfig;

        // 如果插件提供了非空的默认配置对象
        if (defaultConfig != null)
        {
            try
            {
                // 获取配置文件所在的目录路径
                var configDir = Path.GetDirectoryName(configPath);

                // 检查目录路径是否有效且目录是否存在，若不存在则创建
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    // 创建配置文件的父目录
                    Directory.CreateDirectory(configDir);
                }

                // 配置 JSON 序列化选项，设置为缩进格式以提高可读性
                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                };

                // 将默认配置对象序列化为 JSON 字符串
                var jsonString = System.Text.Json.JsonSerializer.Serialize(defaultConfig, jsonOptions);

                // 将序列化后的 JSON 字符串写入到配置文件路径
                File.WriteAllText(configPath, jsonString);
                
                // 记录日志，表明已成功生成默认配置文件
                pluginLoaderLogger.LogInformation("Generated default configuration for plugin {PluginName} at {ConfigPath}", plugin.Name, configPath);
            }
            catch (Exception ex)
            {
                // 捕获并记录在生成默认配置文件过程中发生的任何异常
                pluginLoaderLogger.LogError(ex, "Failed to generate default configuration for plugin {PluginName}", plugin.Name);
            }
        }
    }

    // 构建插件专用的 Configuration 对象
    var pluginConfig = new ConfigurationBuilder()
        .AddJsonFile(configPath, optional: true, reloadOnChange: true)
        .Build();

    // 将独立的配置对象传递给插件的服务注册方法
    plugin.RegisterServices(builder.Services, pluginConfig);
}

// 添加基本的 OpenAPI/Swagger 支持
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    // 基本的 Swagger 文档信息
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = apiName,
        Version = apiVersion,
        Description = "一个由插件动态构建的 API"
    });

    // 添加自定义的 ApiKey 安全定义（头部 X-Api-Token）供插件使用受保护路由
    options.AddSecurityDefinition("ApiKeyAuth", new OpenApiSecurityScheme
    {
        Name = "X-Api-Token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "用于访问受保护路由的 API 令牌"
    });

    // 将定义应用到全局（此处不指定特定范围）
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKeyAuth"
                }
            },
            new List<string>()
        }
    });
});

// 构建 WebApplication 实例（此时服务已注册）
var app = builder.Build();

// 全局异常处理：捕获未处理异常并返回统一的 JSON 响应
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var exceptionDetails = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionDetails?.Error;
        // 记录异常（在日志中包含发生路径）
        app.Logger.LogError(exception, "Unhandled exception caught by global handler at {Path}", exceptionDetails?.Path);
        var response = new
        {
            StatusCode = context.Response.StatusCode,
            Message = "An unexpected internal server error has occurred.",

            // 在开发环境中返回详细信息，否则不泄露内部异常消息
            Details = app.Environment.IsDevelopment() ? exception?.Message : null,
            Path = exceptionDetails?.Path
        };
        await context.Response.WriteAsJsonAsync(response);
    });
});

// 在开发环境中启用 Swagger UI，便于调试与查看文档
if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation("Enabling Swagger UI (Development Mode)...");
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", $"{apiName} {apiVersion}");
        options.RoutePrefix = "swagger"; // 以 /swagger 作为 UI 的路由前缀
    });
}

// 让每个插件有机会在请求管道中注册中间件
app.Logger.LogInformation("Configuring plugin middleware...");
foreach (var plugin in plugins)
{
    plugin.Configure(app);
}

// 让每个插件注册它们自身的路由与端点
app.Logger.LogInformation("Registering plugin routes...");
foreach (var plugin in plugins)
{
    // 检查是否启用自动路由前缀
    IEndpointRouteBuilder routeBuilder = app;
    if (plugin.UseAutoRoutePrefix)
    {
        // 默认使用插件名称作为前缀
        string routePrefix = plugin.Name;

        // 尝试从配置中读取重写值 (配置节: RouteOverride:插件名)
        var overrideRoute = app.Configuration.GetValue<string>($"RouteOverride:{plugin.Name}");
        if (!string.IsNullOrEmpty(overrideRoute))
        {
            // 验证重写值只包含字母和数字
            if (System.Text.RegularExpressions.Regex.IsMatch(overrideRoute, "^[a-zA-Z0-9]+$"))
            {
                routePrefix = overrideRoute;
                app.Logger.LogInformation("Route prefix for plugin '{PluginName}' overridden to '{RoutePrefix}'", plugin.Name, routePrefix);
            }
            else
            {
                // 无效重写值，回退到插件名作为路由前缀
                app.Logger.LogWarning("Invalid route override '{OverrideRoute}' for plugin '{PluginName}'. Only alphanumeric characters (A-Z, a-z, 0-9) are allowed. Falling back to default.", overrideRoute, plugin.Name);
            }
        }

        // 如果启用，创建一个带前缀的路由组
        // 使用 TrimStart('/') 确保路径格式正确
        routeBuilder = app.MapGroup($"/{routePrefix.TrimStart('/')}");
    }

    // 将（可能是分组的）路由构建器传递给插件
    plugin.RegisterRoutes(routeBuilder, app.Configuration);
    app.Logger.LogInformation("Loaded Plugin: {PluginName} v{PluginVersion}", plugin.Name, plugin.Version);
}

// 根路径：显示 API 名称、版本与已运行时长
app.MapGet("/", () =>
{
    var uptime = DateTime.UtcNow - startTime;
    return new { apiName, version = apiVersion, runningTime = uptime, message = "Core API running." };
});

// 启动并监听请求
try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// 从 Plugins 目录加载实现了 IApiPlugin 的类型并返回其实例集合
List<IApiPlugin> LoadPlugins(IConfiguration configuration, Microsoft.Extensions.Logging.ILogger logger)
{
    var loadedPlugins = new List<IApiPlugin>();
    string pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");

    if (!Directory.Exists(pluginsPath))
    {
        try
        {
            Directory.CreateDirectory(pluginsPath);
            logger.LogInformation("Plugins directory did not exist and was created at {PluginsPath}", pluginsPath);
        }
        catch (Exception ex)
        {
            // 无法创建目录时记录错误并返回空插件列表
            logger.LogError(ex, "Failed to create plugins directory at {PluginsPath}", pluginsPath);
            return loadedPlugins;
        }
        return loadedPlugins;
    }

    foreach (var dllPath in Directory.GetFiles(pluginsPath, "*.dll"))
    {
        try
        {
            // 使用自定义的 PluginLoadContext 加载插件程序集，实现隔离
            // 每个插件使用单独的 LoadContext，确保依赖隔离
            var loadContext = new PluginLoadContext(dllPath);
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);
            
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IApiPlugin).IsAssignableFrom(t) && !t.IsInterface);

            foreach (var type in pluginTypes)
            {
                if (Activator.CreateInstance(type) is IApiPlugin plugin)
                {
                    loadedPlugins.Add(plugin);
                }
            }
        }
        catch (Exception ex)
        {
            // 插件加载失败时记录错误并继续加载其他插件
            logger.LogError(ex, "Error loading plugin from {DllPath}", dllPath);
        }
    }
    return loadedPlugins;
}
