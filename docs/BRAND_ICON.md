[English](BRAND_ICON.md) | [简体中文](zh-CN/BRAND_ICON.md)

# iStart brand icon

A blue rounded square with a white i and menu lines is shared by Windows and Google themes. The desktop shortcut, taskbar, window, installer, and menu use the same source. Real icons for other apps and files remain unchanged.

- [Figma component](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=15-197) and [size preview](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=15-204).
- Assets/Brand/istart.svg is the 256×256 Figma vector source, with 16 transparent pixels around the edges.
- istart.png is the transparent menu asset. istart.ico contains nine independently rendered SVG sizes: 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixels.
- WPF embeds PNG/ICO; ApplicationIcon embeds the executable icon. The installer embeds the same ICO and installs an independent icon for shortcuts and the uninstall listing.

To maintain assets, install `resvg-py==0.5.0` in a separate Python tooling environment and run scripts/Export-BrandIcon.py. Normal builds, installation, and runtime need no Python, network, or new app dependency. Do not use screenshots as source icons or include Figma's gray preview background in transparent assets.

Validation checked PNG dimensions/corner alpha, the ICO size directory, Release builds, core regression, and both theme layouts. Menu captures confirmed no icon clipping. Windows surfaces share the same configuration; refreshing an existing pinned taskbar icon still depends on Windows caching.
