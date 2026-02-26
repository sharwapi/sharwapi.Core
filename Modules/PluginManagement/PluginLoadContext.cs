using System.Reflection;
using System.Runtime.Loader;

namespace sharwapi.Core.Modules.PluginManagement;

/// <summary>
/// 自定义程序集加载上下文，用于实现插件的隔离加载。
/// 每个插件在独立的 AssemblyLoadContext 实例中运行，防止依赖冲突。
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    /// <summary>
    /// 依赖解析器，用于根据 .deps.json 文件解析依赖程序集的路径。
    /// </summary>
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// 初始化 PluginLoadContext 的新实例。
    /// </summary>
    /// <param name="pluginPath">插件主程序集文件的完整路径。</param>
    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <summary>
    /// 重写 Load 方法以自定义程序集加载逻辑。
    /// </summary>
    /// <param name="assemblyName">程序集名称。</param>
    /// <returns>加载的程序集实例，如果无法解析则返回 null。</returns>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 确保 sharwapi.Contracts.Core 程序集不被当前上下文加载。
        // 这保证了宿主应用程序和插件使用相同的 IApiPlugin 接口类型，
        // 避免因类型加载上下文不同而导致的类型转换异常。
        if (assemblyName.Name == "sharwapi.Contracts.Core")
        {
            return null;
        }

        // 尝试使用依赖解析器将程序集名称解析为文件路径。
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    /// <summary>
    /// 重写 LoadUnmanagedDll 方法以自定义非托管库加载逻辑。
    /// </summary>
    /// <param name="unmanagedDllName">非托管库名称。</param>
    /// <returns>加载的库指针，如果无法解析则返回 IntPtr.Zero。</returns>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
