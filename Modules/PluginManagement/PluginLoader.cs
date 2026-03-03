using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using sharwapi.Contracts.Core;

namespace sharwapi.Core.Modules.PluginManagement;

/// <summary>
/// 插件加载器
/// 负责从 plugins 目录加载实现了 IApiPlugin 的程序集
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
        string pluginsPath = Path.Combine(AppContext.BaseDirectory, "plugins");

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

        // 遍历 plugins 目录下的所有 DLL 文件（单文件插件）
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

        // 遍历 .sharw 插件包，解压到 .cache 后加载
        string cacheRoot = Path.Combine(pluginsPath, ".cache");
        foreach (var sharwPath in Directory.GetFiles(pluginsPath, "*.sharw"))
        {
            try
            {
                var cacheDir = ExtractSharwToCache(sharwPath, cacheRoot);
                if (cacheDir != null)
                {
                    var plugins = LoadPluginFromDirectory(cacheDir);
                    loadedPlugins.AddRange(plugins);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading .sharw plugin from {SharwPath}", sharwPath);
            }
        }

        // 遍历 plugins 目录下的所有子目录（多文件插件，深度为 1），跳过 .cache 缓存目录
        foreach (var subDir in Directory.GetDirectories(pluginsPath)
                     .Where(d => !string.Equals(Path.GetFileName(d), ".cache",
                                 StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var plugins = LoadPluginFromDirectory(subDir);
                loadedPlugins.AddRange(plugins);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading plugin from directory {SubDir}", subDir);
            }
        }

        return loadedPlugins;
    }

    /// <summary>
    /// 从子目录加载多文件插件，返回目录中所有 IApiPlugin 实现。
    /// 优先查找与目录同名的 DLL 作为主程序集；若不存在，则逐一扫描目录内所有 DLL 文件。
    /// AssemblyDependencyResolver 会依据主程序集的 .deps.json 自动解析同目录内的依赖。
    /// </summary>
    /// <param name="dirPath">插件子目录路径</param>
    /// <returns>加载到的所有插件实例列表</returns>
    private List<IApiPlugin> LoadPluginFromDirectory(string dirPath)
    {
        var result = new List<IApiPlugin>();
        var dirName = Path.GetFileName(dirPath);

        // 优先约定：主 DLL 与目录名相同（例如 plugins/MyPlugin/MyPlugin.dll）
        var conventionDllPath = Path.Combine(dirPath, dirName + ".dll");
        if (File.Exists(conventionDllPath))
        {
            _logger.LogDebug("Loading plugin from directory {DirPath} using convention DLL {DllName}.dll", dirPath, dirName);
            var plugin = LoadPluginFromPath(conventionDllPath);
            if (plugin != null) result.Add(plugin);
            return result;
        }

        // 回退：遍历目录内所有 DLL，收集所有包含 IApiPlugin 实现的插件
        _logger.LogDebug("Convention DLL not found in {DirPath}, scanning all DLLs", dirPath);
        foreach (var dllPath in Directory.GetFiles(dirPath, "*.dll"))
        {
            try
            {
                var plugin = LoadPluginFromPath(dllPath);
                if (plugin != null)
                {
                    result.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "DLL {DllPath} in directory does not contain a valid plugin", dllPath);
            }
        }

        if (result.Count == 0)
        {
            _logger.LogWarning("No valid IApiPlugin implementation found in directory {DirPath}", dirPath);
        }

        return result;
    }

    /// <summary>
    /// 将 .sharw 插件包解压到缓存目录，若缓存已是最新则跳过解压。
    /// 解压时执行 Zip Slip 路径合法性检查，防止恶意路径遍历攻击。
    /// </summary>
    /// <param name="sharwPath">.sharw 文件路径</param>
    /// <param name="cacheRoot">缓存根目录路径（plugins/.cache）</param>
    /// <returns>解压后的插件缓存目录路径</returns>
    private string? ExtractSharwToCache(string sharwPath, string cacheRoot)
    {
        var pluginName = Path.GetFileNameWithoutExtension(sharwPath);
        var cacheDir = Path.Combine(cacheRoot, pluginName);
        var sharwModified = File.GetLastWriteTimeUtc(sharwPath);

        // 若缓存目录已存在且 .sharw 未更新，跳过解压
        if (Directory.Exists(cacheDir))
        {
            var cacheModified = Directory.GetLastWriteTimeUtc(cacheDir);
            if (sharwModified <= cacheModified)
            {
                _logger.LogDebug("Cache for {PluginName} is up-to-date, skipping extraction", pluginName);
                return cacheDir;
            }
            _logger.LogInformation("Updating cache for {PluginName}", pluginName);
            Directory.Delete(cacheDir, recursive: true);
        }

        Directory.CreateDirectory(cacheDir);
        var resolvedCache = Path.GetFullPath(cacheDir);

        using var archive = ZipFile.OpenRead(sharwPath);
        foreach (var entry in archive.Entries)
        {
            // Zip Slip 防护：确保解压路径不超出缓存目录
            var destPath = Path.GetFullPath(Path.Combine(resolvedCache, entry.FullName));
            if (!destPath.StartsWith(resolvedCache + Path.DirectorySeparatorChar,
                                      StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Zip Slip attempt detected in {SharwPath}: {Entry}", sharwPath, entry.FullName);
                throw new InvalidOperationException($"Zip Slip detected: {entry.FullName}");
            }

            if (string.IsNullOrEmpty(entry.Name)) // 目录条目
            {
                Directory.CreateDirectory(destPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
        }

        // 将缓存目录修改时间与 .sharw 同步，用于下次更新校验
        Directory.SetLastWriteTimeUtc(cacheDir, sharwModified);
        _logger.LogInformation("Extracted .sharw plugin {PluginName} to {CacheDir}", pluginName, cacheDir);
        return cacheDir;
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
