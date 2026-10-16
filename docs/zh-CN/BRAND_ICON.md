[English](../BRAND_ICON.md) | [简体中文](BRAND_ICON.md)

# iStart 统一品牌图标

蓝色圆角底与白色「i + 菜单横线」共用于 Windows、Google 两种主题。桌面快捷方式、任务栏、窗口、托盘、安装器和菜单使用同一源图；普通软件与文件的真实图标保持原样。

- [Figma 组件](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=15-197)；[多尺寸预览](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=15-204)。
- `Assets/Brand/istart.svg` 为 Figma 导出的矢量源，256 × 256，四周透明留白 16 像素。
- `istart.png` 为透明菜单资源；`istart.ico` 包含 16、20、24、32、40、48、64、128、256 像素九档，每档从 SVG 独立渲染。
- WPF 嵌入 PNG/ICO，主程序通过 ApplicationIcon 编译图标；托盘克隆 ICO 后释放读取流，退出时释放图标。安装器嵌入同一 ICO，并安装独立图标文件供快捷方式、卸载列表使用。

维护资源时，可在独立 Python 工具环境安装 `resvg-py==0.5.0` 并运行 `scripts/Export-BrandIcon.py`。正常构建、安装及运行不需要 Python、网络或新增应用依赖。不要使用截图当图标源；Figma 预览的灰色画布背景不应进入透明资源。

本次验证：透明 PNG 尺寸与角落 alpha、多尺寸 ICO 目录、Release 构建及既有核心/双主题布局回归通过。菜单截图确认新图标无裁切；系统各入口由同一资源配置，已有任务栏固定项是否即时刷新仍取决于 Windows 缓存。
