using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;
using sharwapi.Contracts.Core;
using System.IO;

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

        // 确保插件专属 data 目录存在
        EnsurePluginDataDirectory(plugin);

        // 为插件构建独立的配置对象，支持热重载
        var pluginConfig = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: true, reloadOnChange: true)
            .Build();

        // 调用插件的服务注册方法
        plugin.RegisterServices(_services, pluginConfig);
    }

    /// <summary>
    /// 确保插件专属 data 目录存在
    /// </summary>
    private void EnsurePluginDataDirectory(IApiPlugin plugin)
    {
        var dataDir = plugin.DataDirectory;
        if (!Directory.Exists(dataDir))
        {
            try
            {
                Directory.CreateDirectory(dataDir);
                _logger.LogInformation("Created data directory for plugin {PluginName} at {DataDir}", plugin.Name, dataDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create data directory for plugin {PluginName} at {DataDir}", plugin.Name, dataDir);
            }
        }
    }

    /// <summary>
    /// 确保插件配置文件存在并保持与 DefaultConfig 同步
    /// 如果配置文件不存在，从插件的 DefaultConfig 生成默认配置；
    /// 如果已存在，则递归合并 DefaultConfig 中缺失的键，保留用户已有的值。
    /// </summary>
    private void EnsurePluginConfigFile(IApiPlugin plugin, string configPath)
    {
        // 确保配置目录存在
        var configDir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        // 获取插件提供的默认配置
        // 注意：如果 DefaultConfig 抛出异常，属于插件自身的问题，此处不做 catch
        var defaultConfig = plugin.DefaultConfig;
        if (defaultConfig == null) return;

        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        };

        if (!File.Exists(configPath))
        {
            // 配置文件不存在，直接写入 DefaultConfig
            try
            {
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
        else
        {
            // 配置文件已存在，读取并与 DefaultConfig 递归合并缺失的键
            try
            {
                var existingJson = File.ReadAllText(configPath);
                var existingNode = JsonNode.Parse(existingJson) as JsonObject;
                var defaultJsonString = System.Text.Json.JsonSerializer.Serialize(defaultConfig);
                var defaultNode = JsonNode.Parse(defaultJsonString) as JsonObject;

                if (existingNode != null && defaultNode != null)
                {
                    MergeMissingKeys(existingNode, defaultNode);
                    var mergedJson = existingNode.ToJsonString(jsonOptions);

                    if (TryWriteConfigWithTimeout(configPath, mergedJson, TimeSpan.FromSeconds(10)))
                    {
                        _logger.LogInformation("Merged default configuration for plugin {PluginName} at {ConfigPath}", plugin.Name, configPath);
                    }
                    else
                    {
                        _logger.LogError("Timed out while merging configuration for plugin {PluginName} at {ConfigPath}", plugin.Name, configPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to merge configuration for plugin {PluginName}, keeping existing config file unchanged", plugin.Name);
            }
        }
    }

    /// <summary>
    /// 递归合并默认配置到现有配置中，仅补充缺失的键，不覆盖已有值也不删除多余键。
    /// </summary>
    /// <param name="existing">现有配置的 JsonObject</param>
    /// <param name="defaults">默认配置的 JsonObject</param>
    private static void MergeMissingKeys(JsonObject existing, JsonObject defaults)
    {
        // 构建现有配置键的大小写不敏感查找表
        // 用于检测 DefaultConfig 中的键在现有配置中是否存在但大小写不同
        var existingKeyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in existing)
        {
            existingKeyMap[kvp.Key] = kvp.Key;
        }

        foreach (var kvp in defaults)
        {
            if (kvp.Value == null) continue;

            if (existing.ContainsKey(kvp.Key))
            {
                // 键存在且大小写完全匹配
                if (kvp.Value is JsonObject defaultObj && existing[kvp.Key] is JsonObject existingObj)
                {
                    // 双方都是嵌套对象 → 递归合并
                    MergeMissingKeys(existingObj, defaultObj);
                }
                // 其他情况（值类型、数组、混合类型等）→ 保留现有值，不做任何操作
            }
            else if (existingKeyMap.ContainsKey(kvp.Key))
            {
                // 存在大小写不同但实质相同的键 → 沿用现有键名
                var actualKey = existingKeyMap[kvp.Key];
                if (kvp.Value is JsonObject defaultObj && existing[actualKey] is JsonObject existingObj)
                {
                    // 双方都是嵌套对象 → 递归合并到已有的键名下
                    MergeMissingKeys(existingObj, defaultObj);
                }
                // 值类型或类型不一致 → 保留现有值，不做任何操作
            }
            else
            {
                // 键完全不存在 → 添加（使用 DefaultConfig 中的键名大小写）
                existing[kvp.Key] = kvp.Value.DeepClone();
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
