# 参与开发

谢谢你想帮忙。这个项目很小，规则也简单。

## 它是什么

一个 Windows 托盘小工具，用两个底层钩子（`WH_MOUSE_LL` / `WH_KEYBOARD_LL`）在输入到达目标程序之前
把它拦下来，从而实现「禁用」或「换绑」那些改不掉的快捷键。

只用了 .NET Framework 自带的东西，**没有第三方依赖**，也不需要 Visual Studio。

## 编译

```powershell
.\build.ps1            # 编译主程序 + 生成安装包
.\build.ps1 -Run       # 顺便跑一下
.\build.ps1 -NoSetup   # 只编译主程序
```

需要 Windows 自带的 `csc.exe`（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`），Win10/11 都有。

## 代码在哪儿

| 文件 | 职责 |
|---|---|
| `src/Interceptor.cs` | 两个钩子、规则匹配、快捷键转发。改行为先看这里 |
| `src/Rules.cs` | 规则模型、修饰键组合、VK 名表、序列化格式 |
| `src/Config.cs` | INI 读写、开机启动、旧版本迁移 |
| `src/App.cs` | 托盘、全局热键、隐藏窗口、单实例 |
| `src/SettingsForm.cs` | 设置界面（都是手写布局，没有设计器文件） |
| `installer/Setup.cs` | 单文件安装包 + 卸载程序 |

## 改动约定

- 保持 **C# 5** 语法（`csc.exe` 是老的，不要用 `?.`、字符串插值、表达式体成员）
- 不要引入 NuGet 包或第三方 DLL
- 界面文案用中文，尽量说人话，别写成「该功能暂不可用」那种
- 改了行为就顺手更新 README 的对应段落和 `更新日志`

## 提 PR 之前

1. `.\build.ps1` 能过
2. 在本机真跑一遍，别只编译
3. 别把 `dist/` 或 `*.exe` 提交上来

## 提 issue 之前

先看 README 的「已知限制」，尤其是这两条：

- 目标程序以管理员身份运行时拦不住（Windows 权限隔离）
- Ctrl+拖拽 被拦之后，自由变换里的「Ctrl 拖角 = 扭曲」也会一起被挡

## 许可

提交的代码默认按 [MIT](LICENSE) 授权。
