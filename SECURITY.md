# Security Policy

## 报告安全问题

如果你发现安全问题（例如命令注入、权限绕过、规则误伤非目标程序等），请**不要**直接开公开 Issue。

- 优先使用 GitHub 的 **Private vulnerability reporting**（仓库 → Security → Report a vulnerability）。
- 或在 Issue 中只描述「存在安全问题，希望私下沟通」，维护者会联系你。

请尽量提供：复现步骤、Windows 版本、程序版本、相关日志（`%LOCALAPPDATA%\SteamOfflineSwitch\app.log`）。

## 安全模型与已知边界

- 本程序需要**管理员权限**，因为它要修改 Windows 防火墙规则。请只从本仓库 Releases 或自行编译的版本运行。
- 程序**不联网**、不上传任何数据；仅在本地读写设置与日志（`%LOCALAPPDATA%\SteamOfflineSwitch\`）。
- 程序只新增/启停名称前缀为 `SteamOfflineSwitch_` 的防火墙规则，删除操作也仅限于这些规则。
- 程序不安装服务、不加载驱动、不注入其他进程、不修改游戏文件。
- 未做代码签名，可能触发 SmartScreen「未知发布者」提示；介意者可自行编译。

## 支持的版本

仅对最新发布版本提供安全修复。
