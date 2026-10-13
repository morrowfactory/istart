"""将 Figma 导出的 SVG 生成透明 PNG 和多尺寸 ICO（仅设计维护时运行）。"""
from pathlib import Path
import struct
import resvg_py

brand = Path(__file__).resolve().parents[1] / "src/IStart.App/Assets/Brand"
sizes = (16, 20, 24, 32, 40, 48, 64, 128, 256)
frames = [resvg_py.svg_to_bytes(svg_path=str(brand / "istart.svg"), width=s, height=s)
          for s in sizes]
(brand / "istart.png").write_bytes(frames[-1])
offset = 6 + 16 * len(sizes)
directory = bytearray(struct.pack("<HHH", 0, 1, len(sizes)))
for size, frame in zip(sizes, frames):
    directory.extend(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0,
                                 1, 32, len(frame), offset))
    offset += len(frame)
(brand / "istart.ico").write_bytes(directory + b"".join(frames))
