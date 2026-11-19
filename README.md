# Desktop Boxes 1.0 桌面预览版

在 Windows 10/11 桌面按路径归类文件、文件夹和快捷方式。运行期间不需要网络。程序使用 C#、Windows Forms 和 Windows 桌面接口，使用系统自带的 .NET Framework 4.x。

## 下载与使用

在 Releases 下载 DesktopBoxes.exe 后直接运行。需要开始菜单入口及卸载功能时，使用 DesktopBoxes-Setup.exe。以普通用户身份运行，以便接收资源管理器的拖放。便携版与安装版共用当前用户的布局，设置在 %LOCALAPPDATA%\DesktopBoxes\layout.xml。修改后自动写入，上一版保存在同目录 .bak 文件。可通过通知区域退出。

拖动标题移动文件框，拖动边缘改变大小，位置按 8 像素网格吸附。拖入项目后双击打开。右键修改标题、颜色、不透明度和视图，或移除引用。删除文件框保留磁盘上的原文件。可从托盘新建文件框或重新连接桌面。

## 桌面与视觉

文件框挂载到 Explorer 的 SHELLDLL_DefView 子窗口，显示前检查实际父窗口。默认采用整体透明。每个框可单独开启实验性的系统模糊，Windows 接受接口调用仍需目视检查背景效果。Explorer 重启或显示器配置改变后通过系统消息重连。找不到桌面时短暂重试，空闲时不轮询。

## 测试与构建

在 Windows x64 上安装 Inno Setup 6，从 PowerShell 运行 .\Build.ps1。输出位于 out。构建会测试布局序列化、替换及备份，静默安装、已安装文件哈希、已安装程序自测及卸载。EXE 未进行 Authenticode 签名。

启动程序并让其空闲，在 PowerShell 中运行 .\Measure-DesktopBoxes.ps1 > idle.json。分别用一个空框及五框共 500 个引用采样。输出包含系统版本、采样时长、进程工作集、专用内存和占全机容量的平均 CPU 百分比。测试 Windows 10/11 时应一并记录框数、引用数、屏幕缩放与模糊设置。

桌面验收需在交互式 Explorer 会话进行。用记事本遮挡文件框，再按两次 Win+D。测试拖放、失效路径、关机重启后布局恢复；重启 Explorer，确认框能重新出现且可操作。对照细节明显的壁纸检查模糊开关；混合 DPI、多屏及断开副屏需要分别验证。CI 使用 Windows Server，不提供客户端系统上的观感和资源数据。
