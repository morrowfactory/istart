// Maintenance only: rasterize the unchanged Figma SVGs for WPF; no runtime SVG dependency.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const root = path.join(__dirname, '..', 'src', 'IStart.App', 'Assets', 'Icons');
(async () => {
  for (const name of fs.readdirSync(root).filter(name => name.endsWith('.svg'))) {
    await sharp(path.join(root, name), { density: 144 }).png().toFile(path.join(root, name.replace('.svg', '.png')));
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
