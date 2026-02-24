using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using sharwapi.Contracts.Core;

namespace sharwapi.Core.Modules.PluginManagement;

/// <summary>
/// 插件加载器
/// 负责从 Plugins 目录加载实现了 IApiPlugin 的程序集
/// </summary>
public class PluginLoader
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;

    /// <summary>
    /// 初始化插件加载器
    /// </summary>
    /// <param name="configuration">应用程序配置</param>
    /// <param name="logger">日志记录器</param>
    public PluginLoader(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 加载所有插件
    /// </summary>
    /// <returns>加载的插件列表</returns>
    public List<IApiPlugin> LoadPlugins()
    {
        var loadedPlugins = new List<IApiPlugin>();
        
        // 获取插件目录路径
        string pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");

        // 检查插件目录是否存在，如果不存在则创建
        if (!Directory.Exists(pluginsPath))
        {
            try
            {
                Directory.CreateDirectory(pluginsPath);
                _logger.LogInformation("Plugins directory did not exist and was created at {PluginsPath}", pluginsPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create plugins directory at {PluginsPath}", pluginsPath);
            }
            return loadedPlugins;
        }

        // 遍历 Plugins 目录下的所有 DLL 文件
        foreach (var dllPath in Directory.GetFiles(pluginsPath, "*.dll"))
        {
            try
            {
                // 尝试从每个 DLL 加载插件
                var plugin = LoadPluginFromPath(dllPath);
                if (plugin != null)
                {
                    loadedPlugins.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading plugin from {DllPath}", dllPath);
            }
        }

        return loadedPlugins;
    }

    /// <summary>
    /// 从指定路径加载单个插件
    /// </summary>
    /// <param name="dllPath">DLL 文件路径</param>
    /// <returns>插件实例，如果加载失败则返回 null</returns>
    private IApiPlugin? LoadPluginFromPath(string dllPath)
    {
        // 使用隔离加载上下文加载程序集，防止插件之间的依赖冲突
        var loadContext = new PluginLoadContext(dllPath);
        var assembly = loadContext.LoadFromAssemblyPath(dllPath);
        
        // 从程序集中查找所有实现了 IApiPlugin 接口的类型
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IApiPlugin).IsAssignableFrom(t) && !t.IsInterface);

        // 遍历找到的类型，尝试实例化第一个成功的类型
        foreach (var type in pluginTypes)
        {
            if (Activator.CreateInstance(type) is IApiPlugin plugin)
            {
                return plugin;
            }
        }

        return null;
    }
}
