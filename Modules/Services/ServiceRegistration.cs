using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using sharwapi.Contracts.Core;

namespace sharwapi.Core.Modules.Services;

/// <summary>
/// 插件服务注册器
/// 负责将插件的服务注册到 DI 容器
/// </summary>
public class PluginServiceRegistrar
{
    private readonly IServiceCollection _services;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    /// <summary>
    /// 初始化服务注册器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="logger">日志记录器</param>
    public PluginServiceRegistrar(IServiceCollection services, Microsoft.Extensions.Logging.ILogger logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// 注册所有插件的服务
    /// </summary>
    /// <param name="plugins">已加载的插件列表</param>
    /// <param name="configuration">应用程序配置</param>
    public void RegisterPluginServices(List<IApiPlugin> plugins, IConfiguration configuration)
    {
        _logger.LogInformation("Registering plugin services...");

        foreach (var plugin in plugins)
        {
            RegisterPluginServices(plugin, configuration);
        }
    }

    /// <summary>
    /// 注册单个插件的服务
    /// </summary>
    private void RegisterPluginServices(IApiPlugin plugin, IConfiguration configuration)
    {
        // 构建插件配置文件的完整路径
        var configPath = Path.Combine(AppContext.BaseDirectory, "config", $"{plugin.Name}.json");
        
        // 确保配置文件存在，如果不存在则从插件的默认配置生成
        EnsurePluginConfigFile(plugin, configPath);

        // 为插件构建独立的配置对象，支持热重载
        var pluginConfig = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: true, reloadOnChange: true)
            .Build();

        // 调用插件的服务注册方法
        plugin.RegisterServices(_services, pluginConfig);
    }

    /// <summary>
    /// 确保插件配置文件存在
    /// 如果配置文件不存在，从插件的 DefaultConfig 生成默认配置
    /// </summary>
    private void EnsurePluginConfigFile(IApiPlugin plugin, string configPath)
    {
        // 检查配置文件是否已存在
        if (!File.Exists(configPath))
        {
            // 获取插件提供的默认配置
            var defaultConfig = plugin.DefaultConfig;

            if (defaultConfig != null)
            {
                try
                {
                    // 确保配置目录存在
                    var configDir = Path.GetDirectoryName(configPath);

                    if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }

                    // 配置 JSON 序列化选项为缩进格式，便于阅读
                    var jsonOptions = new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                    // 将默认配置序列化为 JSON 并写入文件
                    var jsonString = System.Text.Json.JsonSerializer.Serialize(defaultConfig, jsonOptions);
                    if (TryWriteConfigWithTimeout(configPath, jsonString, TimeSpan.FromSeconds(10)))
                    {
                        _logger.LogInformation("Generated default configuration for plugin {PluginName} at {ConfigPath}", plugin.Name, configPath);
                    }
                    else
                    {
                        _logger.LogError("Timed out while generating default configuration for plugin {PluginName} at {ConfigPath}", plugin.Name, configPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate default configuration for plugin {PluginName}", plugin.Name);
                }
            }
        }
    }

    /// <summary>
    /// 添加 Swagger/OpenAPI 服务
    /// </summary>
    /// <param name="apiName">API 名称</param>
    /// <param name="apiVersion">API 版本</param>
    public void AddSwaggerServices(string apiName, string apiVersion)
    {
        _services.AddEndpointsApiExplorer();
        
        _services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = apiName,
                Version = apiVersion,
                Description = "一个由插件动态构建的 API"
            });

            options.AddSecurityDefinition("ApiKeyAuth", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Name = "X-Api-Token",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                Description = "用于访问受保护路由的 API 令牌"
            });

            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "ApiKeyAuth"
                        }
                    },
                    new List<string>()
                }
            });
        });
    }
    /// <summary>
    /// 以超时保护写入配置文件，避免启动阶段因 I/O 卡顿而无限阻塞。
    /// </summary>
    private static bool TryWriteConfigWithTimeout(string configPath, string jsonContent, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            var writeTask = WriteConfigAsync(configPath, jsonContent, cts.Token);
            writeTask.GetAwaiter().GetResult();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task WriteConfigAsync(string configPath, string jsonContent, CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(
            configPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);

        var bytes = System.Text.Encoding.UTF8.GetBytes(jsonContent);
        await fileStream.WriteAsync(bytes, cancellationToken);
        await fileStream.FlushAsync(cancellationToken);
    }
}
