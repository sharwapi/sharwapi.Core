# 贡献指南 (Contributing Guide)

[简体中文](/CONTRIBUTING_CN.md) | [English](/CONTRIBUTING.md)

感谢您对 **SharwAPI (Core API)** 感兴趣！

我们非常欢迎社区通过 Issue 或 Pull Request 参与贡献。在开始之前，请阅读以下指南。

## 开发环境准备

在开始编写代码之前，请确保您的开发环境满足以下要求：

* **操作系统**: Windows x64 / Linux x64
* **开发工具**:
    * [Visual Studio 2022](https://visualstudio.microsoft.com/zh-hans/vs/) 或 [Visual Studio Code](https://code.visualstudio.com/)
    * [Git](https://git-scm.org/)
* **SDK**: [.NET 9 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)

## 获取源码与构建

SharwAPI 的开发通常涉及 **API 本体** 和 **接口层** 两个仓库。请按照以下步骤搭建本地开发环境。

### 1. 建立工作区

建议创建一个统一的解决方案来管理项目：

```bash
mkdir sharwapi
cd sharwapi
dotnet new sln --name sharwapi

```

### 2. 拉取代码

您需要同时拉取 Core 和 Contracts 仓库（因为 Core 依赖 Contracts）：

```bash
# 拉取 API 本体
git clone https://github.com/sharwapi/sharwapi.Core.git

# 拉取 接口层
git clone https://github.com/sharwapi/sharwapi.Contracts.Core.git

```

### 3. 添加到解决方案

```bash
dotnet sln sharwapi.sln add sharwapi.Core/sharwapi.Core.csproj
dotnet sln sharwapi.sln add sharwapi.Contracts.Core/sharwapi.Contracts.Core.csproj

```

### 4. 构建与运行

您可以直接使用 CLI 运行项目进行测试：

```bash
# 进入 Core 目录
cd sharwapi.Core
# 运行
dotnet run

```

或者发布为可执行文件：

```bash
dotnet publish -c Release

```

构建产物通常位于 `bin/Release/net9.0/publish`。

## 架构理解

在贡献代码前，请务必理解 SharwAPI 的 **微内核 (Microkernel)** 设计理念。API 本体应保持轻量，避免耦合具体的业务逻辑。

可参考 [在线文档中的架构一章](https://sharwapi.hope-now.top/architecture/overview)

## 提交规范

1. **Fork** 本仓库到您的 GitHub 账户。
2. **Clone** 您 Fork 的仓库到本地。
3. 创建新的 **分支** 进行开发 (例如 `feat/optimize-loader` 或 `fix/logger-bug`)。
4. 确保代码风格符合 C# 标准规范，并能通过编译。
5. 提交 Commit 并 Push 到您的远程仓库。
6. 发起 **Pull Request (PR)** 到本仓库的 `main` 分支。

## 开源协议

SharwAPI 本体 (Core API) 基于 [GNU General Public License v3.0](/LICENSE) 获得许可

这也意味着您对本项目的所有修改都应该按照 [GNU General Public License v3.0](/LICENSE) 进行开源