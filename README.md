# ZZZ Switcher · 绝区零 官服 / B服 通道切换器

在**同一个已安装的绝区零 PC 客户端**上，在官服（米哈游账号）与 B 服（哔哩哔哩账号）之间双向切换：只改 `config.ini` 里的通道字段，不下载资源、不替换 DLL、不碰校验清单。

产物：`dist\ZZZSwitcher.exe`（单文件，35 328 B，.NET Framework 4.x，Windows 10/11 自带运行时）。

---

## 1. 为什么不能只改 config.ini

早期版本（`LaunchOfficial.exe` / `LaunchBilibili.exe`，源码 `channel_launcher.cs`）假设"两个渠道只差一个 config.ini"。对**两台真实安装**做逐文件比对后，这个假设不成立：

| 差异项 | 官服目录实测 | B服目录实测 |
|---|---|---|
| `config.ini` 通道三元组 | `channel=1 / sub_channel=1 / cps=zzz_gw_pc` | `channel=14 / sub_channel=0 / cps=zzz_bilibili_pc` |
| 渠道 SDK | **完全没有** | `PCGameSDK.dll` 5 905 408 B + `BLPlatform64\` 72 文件 / 180.92 MB |
| `plugin_sdk_version` | 无此键 | `5.0.4` |
| 核心程序 | `ZenlessZoneZero.exe`、`GameAssembly.dll`、`UnityPlayer.dll`、`mhypbase.dll` | 与官服**哈希完全相同** |
| 版本账本 | `version_info = CNPRODWin3.3.0`，`pkg_version` 12 892 行 | 同上，`pkg_version` 与本目录**哈希相同** |

结论两条：

- 客户端本体是同一套 → 切通道不需要重装、不需要重新下载 70 GB。
- B 服额外带一套 187 MB 的 B 站登录 SDK → **从官服目录切到 B 服，必须先有这套 SDK**，否则登录界面拿不到 B 站渠道实现。

## 2. 渠道真值表

程序里唯一真相来源是 `src/ChannelProfile.cs`，数值来自实测：

| 项 | 官服 | B服 |
|---|---|---|
| `channel` | `1` | `14` |
| `sub_channel` | `1` | `0` |
| `cps` | `zzz_gw_pc` | `zzz_bilibili_pc` |
| `plugin_sdk_version` | 不应存在 | `5.0.4`（取自 `PCGameSDK.dll` FileVersion） |
| 渠道 SDK 目录 | 不需要 | `ZenlessZoneZero_Data\Plugins\x86_64\BLPlatform64` |
| 启动器注册表 schema | `HKCU\Software\miHoYo\HYP\1_1\nap_cn` | `HKCU\Software\miHoYo\HYP\standalone\14_0\nap_cn\xV0f4r1GT0\nap_cn` |

> 旧 README 把官服 `cps` 写成 `zzz_mktbackup2_pc`，实测是 `zzz_gw_pc`；旧模板把段名写成小写 `[general]`，实际是 `[General]`。两处都是错的，本版本已按实测更正。

## 3. 快速开始

1. 把 `dist\ZZZSwitcher.exe` 放到任意目录（不必放进游戏目录）。
2. 双击运行，UAC 点「是」（写 `Program Files` 下的游戏目录需要管理员）。
3. 界面会**自动从注册表检测**已安装的游戏目录（也记住上次用过的），检测不到再点路径框选「浏览…」，或直接从资源管理器把目录拖进窗口。
4. 顶部大字显示当前渠道（左边缘竖条与圆点同色：官服蓝 / B 服粉），下面 4 行状态：版本账本 / B 站 SDK / 启动器归属 / 游戏与占用。
5. 点「切到官服并启动」或「切到 B 服并启动」。勾选「只切换，不启动游戏」则只改配置。当前渠道的按钮会变成描边样式并显示「✓ 当前是…」，再点一次等于「确认后直接启动游戏」。
6. 底部深色日志区随操作实时滚动（右侧细滑条可拖动，滚轮/PageUp/PageDown 均可；只有贴在底部时才自动跟随，往上翻不会被新日志打断），右键可复制全部或打开 `switch.log`；「操作 → 试运行」只算不写。

界面速查：

| 操作 | 快捷键 / 方式 |
|---|---|
| 切到官服并启动 | `Ctrl+1` |
| 切到 B 服并启动 | `Ctrl+2` |
| 试运行（不写入） | `Ctrl+3` |
| 重新检测 | `F5` |
| 选择目录 / 自动检测 | `Ctrl+O` / `Ctrl+D` |
| 换目录（最近 8 个） | 点顶部路径框 |
| 复制诊断信息到剪贴板 | `Ctrl+Shift+C` |
| 结束游戏进程 | 操作 → 结束游戏进程 |
| DX12 启动开关 | 勾选「DX12 启动」/ `Ctrl+0` |
| 启动游戏（不改配置） | `Ctrl+Enter` |

窗口位置、大小、最近目录与 DX12 开关记在同目录 `ui.ini`，下次打开沿用（`ui.ini` 里的坐标如果落在所有屏幕之外会被忽略，窗口回到居中）。

### DX12 启动（实测结论）

「DX12 启动」勾选后，启动游戏时会带上 `-use-d3d12`。这个参数是**逐个对照实测**出来的，别用常见的 `-force-d3d12`：

| 参数 | 结果（`Player.log`） |
|---|---|
| `-use-d3d12` | `Forcing GfxDevice: Direct3D 12` + `d3d12: loaded!` + `Direct3D 12 [level 12.1]` ✅ |
| `-force-d3d12` | `Direct3D 11.0 [level 11.1]`，无 `Forcing` 行（被忽略） |
| `--force-d3d12` | 同上，D3D11 |
| `-d3d12` | 同上，D3D11 |
| `-force-d3d11` | `Forcing GfxDevice: Direct3D 11`（对照组，证明参数解析正常） |

- 本机实测后端：`NVIDIA GeForce RTX 4060 Ti`、`Direct3D 12 [level 12.1]`、VRAM 7949 MB、`[streamline] DLSS 310.7.0`。
- 引擎是 Unity 2019.4.40f1；开关由 `UnityPlayer.dll` 解析，`ZenlessZoneZero.exe` 只是透传。
- CLI 等价写法：`ZZZSwitcher.exe --launch --dx12 --dir "H:\ZenlessZoneZero Game"`。
- 开关只影响**启动参数**，不改 `config.ini`、不写游戏目录；关掉即回到客户端默认后端。勾选状态存在 `ui.ini` 的 `dx12=1`。

### 本机部署现状（2026-10-31 合并后）

主目录 `H:\ZenlessZoneZero Game` 已合并成「双渠道超集」，其余目录可只当备份留着：

| 项目 | 来源 | 结果 |
|---|---|---|
| `Plugins\x86_64\PCGameSDK.dll` | B 服 | 5.63 MB，SHA-256 与源一致 |
| `Plugins\x86_64\BLPlatform64\` | B 服 | 72 文件 / 180.92 MB，逐文件哈希一致 |
| `Plugins\x86_64\license.txt` | B 服 | 一致 |
| `sdk_pkg_version` | B 服 | 71 条清单全部在本地、尺寸全对 |
| `Audio\Windows\Full\Jp\` + `Audio_Japanese_pkg_version` | B 服 | 78 文件 / 4.83 GB，与原有 `Full\Cn` 并存 |
| `config.ini` | 工具切换 | `channel=14 / sub_channel=0 / cps=zzz_bilibili_pc / plugin_sdk_version=5.0.4` |

- 合并前官服原配置备份：`H:\ZenlessZoneZero Game\switch-backup\merge-20261031-224939\config.ini.official-before-merge`。
- 切换事务备份：`switch-backup\config.ini.20261031-225103.official-to-bilibili`（含 `.sha256`）。
- `dist\launcher.ini` 与 `dist\ui.ini` 已指向 `H:`，工具打开即是主目录。
- 未搬运的只有按需流式缓存（`Blocks` / `Video` / `webCaches` 差集，约 1 200 个文件），首次进对应场景时客户端自行补下载。
- 更新归属仍是官服启动器（注册表 `HYP\1_1\nap_cn` 指向 `H:`）；它跑一次更新会把 `config.ini` 写回官服，重新点一次「切到 B 服」即可。

## 4. 命令行

```bat
ZZZSwitcher.exe                                  :: 打开界面（默认）
ZZZSwitcher.exe --check                          :: 只读状态（人读）
ZZZSwitcher.exe --check --json                   :: 只读状态（机读，单行 JSON）
ZZZSwitcher.exe --switch bilibili                :: 切到 B 服
ZZZSwitcher.exe --switch official --start        :: 切到官服并启动
ZZZSwitcher.exe --switch bilibili --dry-run      :: 只算不写，验证闸门
ZZZSwitcher.exe --switch bilibili --sdk-source "G:\Program Files\ZenlessZoneZerobilibili\games\ZenlessZoneZero Game"
ZZZSwitcher.exe --rollback                       :: 回滚到最近一次备份（校验 SHA-256）
```

`--check --json` 固定形状：

```json
{"dir":"H:\\ZenlessZoneZero Game","exists":true,"channel":1,"channelName":"official","cps":"zzz_gw_pc","gameVersion":"3.3.0","versionInfo":"CNPRODWin3.3.0","versionConsistent":true,"sdkPresent":false,"sdkFiles":0,"sdkSizeMb":0,"owner":"official","canSwitchToBilibili":false,"canSwitchToOfficial":true,"gameRunning":false}
```

退出码：

| 码 | 含义 |
|---|---|
| `0` | ✅ 成功（`--dry-run` 也是 0，日志标"未写盘"） |
| `2` | ⚠️ 版本账本不一致：`version_info` ≠ `game_version`，先更新完再切 |
| `3` | ⚠️ 缺 B 站渠道 SDK，用 `--sdk-source` 或 B 站启动器安装 |
| `4` | ⚠️ 游戏在跑，或 `config.ini` 被占用 |
| `5` | ⚠️ 目录无效 / 缺 `config.ini` |
| `6` | ❌ 写入失败（已自动用备份回滚） |

## 5. 一次切换具体做了什么

| 动作 | 说明 |
|---|---|
| 改 `channel` | 写目标渠道值（`1` / `14`） |
| 改 `sub_channel` | 写 `1` / `0` |
| 改 `cps` | 写 `zzz_gw_pc` / `zzz_bilibili_pc` |
| 切 B 补 `plugin_sdk_version` | 从 `PCGameSDK.dll` 的 FileVersion 取前三段，兜底 `5.0.4` |
| 切官服删 `plugin_sdk_version` | 官服不需要该键；备份里保留原值 |
| 备份 | `switch-backup\config.ini.<yyyyMMdd-HHmmss>.<from>-to-<to>` + 同名 `.sha256` |
| 原子落盘 | 写 `config.ini.tmp` → 内存复核 → `File.Replace` → 磁盘复核，任何一步不符即回滚 |
| 保留不动 | `game_version` / `uapc` / `downloading_mode` / 其余 `plugin_*_version` |
| 绝不触碰 | `version_info` / `pkg_version` / `Audio_*_pkg_version` / `sdk_pkg_version` / 任何资源文件 |

闸门顺序：目录 → 游戏进程与文件占用 → 版本账本 → 渠道 SDK → 写入。任一不满足直接退出，**零写盘**。

## 6. 与旧版的关系

旧版的两个产物 `LaunchOfficial.exe`、`LaunchBilibili.exe` 已删除（2026-10-31），源码 `channel_launcher.cs` 保留作历史参考，标记为 **deprecated**，不再维护。它们的问题（已逐一复现）：

| 旧版行为 | 后果 |
|---|---|
| `ReadChannel` 只认行首 `channel=` | `channel = 14`（等号前有空格）被解析成 `0`，走错分支 |
| 备份交换不做内容比对就 `File.Delete(dst)` | 上述误判时**官服备份被 B 服配置覆盖，原配置永久丢失** |
| 无 `config.ini` 时切 B 用 4 键模板重建 | 丢 `game_version` / `downloading_mode` / `plugin_sdk_version` / `plugin_zqOaT7P3NZ` |
| 官服模式下 `.g` 缺失只 WARN | 目录里直接没有 `config.ini`，游戏拒绝启动 |
| `string.Join(" ", args)` 透传 | 带空格的参数（`-logFile "D:\a b\x.log"`）被拆错 |

## 7. 从源码构建

```bat
build.bat
```

等价命令：

```bat
csc /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 ^
    /win32manifest:launcher.manifest /out:dist\ZZZSwitcher.exe ^
    /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    src\Log.cs src\IniFile.cs src\ChannelProfile.cs src\GameFolder.cs ^
    src\ChannelSdk.cs src\LauncherMap.cs src\Switcher.cs src\Ui.cs src\Program.cs
```

编译器：`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（C# 5 语言级别，不引入任何第三方包）。
输出：`dist\ZZZSwitcher.exe`，无警告无错误，73 216 B。

## 8. 手工验收清单（按顺序跑）

工具本身不启动、不测试，验证由使用者手动执行：

1. `ZZZSwitcher.exe --check` → 应打印 7 行状态，退出码 `0`。
2. `ZZZSwitcher.exe --check --json` → 单行 JSON，字段齐全。
3. `ZZZSwitcher.exe --switch official --dry-run` → 打印计划动作，退出码 `0`，`config.ini` 未被改动（对比修改时间）。
4. 真跑前先关游戏；`--switch bilibili` → 退出码 `0`，目录内出现 `switch-backup\config.ini.<时间戳>...` 与 `.sha256`。
5. `--check` 复核：`channelName` 已变，`versionConsistent` 仍为 `true`。
6. 启动游戏确认登录页出现 B 站账号登录；退出后用 `--rollback` 回到官服并再次 `--check`。

## 9. 已知边界

- **启动器会覆写 `config.ini`**：用官方或 B 站启动器启动时，启动器按自己的 `config.ini` 与注册表 schema 回写通道字段。手动切换后再用启动器启动，会被改回启动器自己的渠道——这是渠道设计，不是本工具的 bug。
- **两渠道发版不同步**：先发版的一侧会把 `version_info` 与资源推进到新版本，另一侧启动器随后会判定"版本不符"并回滚或重新下载。工具只做检测（退出码 `2`）与提示，不伪造版本、不改 `version_info`、不下补丁。
- **资源集天然不同**：ZZZ PC 是按需流式下载，两个目录的 `.blk` 缓存子集不一样（实测官服独有 899 文件、B 服独有 1138 文件，语音包一侧 `Full\Cn`、一侧 `Full\Jp`）。切换后首次进入某些内容会触发增量下载，属正常。
- **需要管理员**：写 `Program Files` 下目录需要提权，`launcher.manifest` 声明 `requireAdministrator`，因此每次启动都会弹 UAC。
- **不联网**：工具不发任何网络请求；SDK 补齐只从本机已有目录复制。

## 10. 文件清单

| 文件 | 作用 |
|---|---|
| `src/Program.cs` | 参数路由、CLI 输出、退出码 |
| `src/Log.cs` | 分级日志 `switch.log` + 内存环形缓冲 |
| `src/IniFile.cs` | 无损 INI：保 CRLF / BOM / 段名大小写 / 行序，只改单行 |
| `src/ChannelProfile.cs` | 渠道真值表 |
| `src/GameFolder.cs` | 目录解析、进程与占用检测、版本账本读取 |
| `src/ChannelSdk.cs` | B 站 SDK 检测、版本推导、从已装目录复制 |
| `src/LauncherMap.cs` | 注册表归属（只读）、启动器 exe 探测 |
| `src/Switcher.cs` | 状态检查、切换事务、备份与回滚、JSON 输出 |
| `src/Ui.cs` | 700×660 可缩放单窗口界面（卡片式布局 + 自绘路径框 + 双渠道按钮 + 深色日志区/自绘滑条 + 菜单/快捷键，`UiPrefs` 持久化窗口、最近目录与 DX12 开关） |
| `src/Theme.cs` | 手绘主题：调色板、圆角卡片与投影、按钮/复选框/路径框/状态栏/深色日志底板与深色滚动条、浅色菜单渲染器 |
| `build.bat` | 静默编译到 `dist\` |
| `launcher.manifest` | `requireAdministrator` + Win10 兼容性声明 + `dpiAware` |
| `docs\ui-preview.png` | 界面截图（离屏渲染自 `dist\ZZZSwitcher.exe` 本体） |
| `channel_launcher.cs` | 旧版源码，deprecated（对应 exe 已删除） |

## 11. 许可证

[MIT](LICENSE)
