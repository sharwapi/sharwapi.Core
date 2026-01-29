using System.Reflection;
using System.Runtime.Loader;

namespace sharwapi.Core
{
    // 自定义程序集加载上下文，用于实现插件的隔离加载。
    // 每个插件在独立的 AssemblyLoadContext 实例中运行，防止依赖冲突。
    public class PluginLoadContext : AssemblyLoadContext
    {
        // 依赖解析器，用于根据 .deps.json 文件解析依赖程序集的路径。
        private readonly AssemblyDependencyResolver _resolver;

        // 初始化 PluginLoadContext 的新实例。
        // pluginPath: 插件主程序集文件的完整路径。
        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            // 初始化 AssemblyDependencyResolver，用于解析插件及其依赖项的路径。
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        // 重写 Load 方法以自定义程序集加载逻辑。
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // 确保 sharwapi.Contracts.Core 程序集不被当前上下文加载。
            // 这保证了宿主应用程序和插件使用相同的 IApiPlugin 接口类型，
            // 避免因类型加载上下文不同而导致的类型转换异常。
            if (assemblyName.Name == "sharwapi.Contracts.Core")
            {
                // 返回 null，委托默认加载上下文（DefaultContext）加载该程序集。
                return null;
            }

            // 尝试使用依赖解析器将程序集名称解析为文件路径。
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            
            if (assemblyPath != null)
            {
                // 如果解析成功，从指定路径加载程序集。
                return LoadFromAssemblyPath(assemblyPath);
            }

            // 如果无法解析路径，返回 null。
            return null;
        }

        // 重写 LoadUnmanagedDll 方法以自定义非托管库加载逻辑。
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            // 尝试解析非托管库的路径。
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            
            if (libraryPath != null)
            {
                // 如果解析成功，从指定路径加载非托管库。
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            // 如果无法解析路径，返回零指针。
            return IntPtr.Zero;
        }
    }
}
