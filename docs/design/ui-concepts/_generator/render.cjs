// Renders every concept's screens to PNG (1600×1000) with headless Chromium.
//   node docs/design/ui-concepts/_generator/render.cjs [concept...]
const path = require('path');
const fs = require('fs');
const { chromium } = require('playwright');

const OUT = path.resolve(__dirname, '..');
const ALL = ['cinema', 'studio', 'hybrid'];

(async () => {
  const wanted = process.argv.slice(2).length ? process.argv.slice(2) : ALL;
  const browser = await chromium.launch();
  const pageObj = await browser.newPage({ viewport: { width: 1600, height: 1000 }, deviceScaleFactor: 1 });
  for (const name of wanted) {
    const concept = require(`./${name}.cjs`);
    const dir = path.join(OUT, concept.name);
    fs.mkdirSync(dir, { recursive: true });
    for (const [file, build] of concept.screens) {
      await pageObj.setContent(build(), { waitUntil: 'load' });
      await pageObj.evaluate(() => document.fonts.ready);
      await pageObj.screenshot({ path: path.join(dir, file) });
      console.log(path.relative(process.cwd(), path.join(dir, file)));
    }
  }
  await browser.close();
})();
