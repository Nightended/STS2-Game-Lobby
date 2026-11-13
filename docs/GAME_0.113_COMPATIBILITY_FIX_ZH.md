# 游戏 0.113.0 客户端兼容修复

本修复以游戏 `v0.113.0` 为目标，修复联机初始化期间的
`InitialGameInfoMessage.gameMode` 缺失异常及同次游戏更新引入的相关接口变化。
客户端清单版本仍为 `0.6.3`。

## 修改范围

- 从普通加入响应、读档响应或重连响应取得游戏模式及自定义标记。
- 使用新版加入请求的标准/Blitz 难度字段，保留原有三种建房选项。
- 按 0.113.0 原生编码顺序补齐兼容协议加入响应中的模式和自定义标记。
- 适配建房菜单的唯一节点名和包含自定义标记的大厅构造函数。
- 适配难度缩放类型及其人数读取方式、遗物分配结果的索引参数。

不修改大厅服务、中继策略、服务地址、配置格式、存档格式、素材或现有玩法选项。
不增加新模式菜单，也不宣称支持其他游戏版本。

## 构建与自动检查

需要仓库 `global.json` 指定的 .NET 9 SDK。将 `Sts2Root` 改为实际游戏安装目录：

```powershell
$env:STS2_TEST_GAME_DATA_DIR = 'D:\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64'

dotnet build sts2-lan-connect/sts2_lan_connect.csproj -c Release `
  '-p:Sts2Root=D:\Steam\steamapps\common\Slay the Spire 2'

dotnet test sts2-lan-connect.Tests/sts2_lan_connect.Tests.csproj -c Release `
  '-p:Sts2Root=D:\Steam\steamapps\common\Slay the Spire 2' `
  --filter 'FullyQualifiedName!~LanConnectInstallerContractTests'

dotnet test sts2-lan-connect.ProtocolPlanTests/sts2_lan_connect.ProtocolPlanTests.csproj -c Release `
  '-p:Sts2Root=D:\Steam\steamapps\common\Slay the Spire 2'
```

`LanConnectGame113CompatibilityTests` 使用游戏实际类型验证加入结果、存档模式标记、
真实难度缩放 IL，以及兼容响应和原生响应的逐位一致性（只允许玩家列表位宽不同）。

Windows 命令排除了明确只支持 POSIX/macOS 的安装器测试。原有许可证测试把原始字符串
与 LF 文本直接比较；Windows 使用 Git 自动转换 CRLF 时，需要让该测试源码以原仓库
LF 换行编译，避免换行差异造成误报。

本次本机检查结果：Release 构建成功（0 错误，1 条原有空值警告）；Windows 普通测试
1269 通过、1 项原有跳过，另排除 8 项 macOS 安装器测试；协议测试 20 通过；
GameAbiLoadCheck 完整加载 1233 个类型。未执行 Godot 场景测试及真实双客户端验收。

## 双人验收

双方完整退出游戏，使用相同修复包并确认游戏版本均为 `v0.113.0`。
替换原来的模组目录，避免在另一个目录中同时留下第二份启用的同 ID 模组。
保留游戏存档和模组用户配置。

1. 新建标准模式空房间，第二位玩家加入并停留，确认没有字段缺失或解码错误。
2. 互换房主重复加入，随后选角色、开局并完成一场战斗。
3. 检查宝箱选取、保存退出、读档加入和断线重连。
4. 使用自定义或每日挑战的玩家，检查原有模式仍能进入正确界面。

自动检查和类型加载检查不能代替真实双客户端联机验收。
若失败，保留双方本次完整 `godot.log`，并注明使用的修复构建和协议。
