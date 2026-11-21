// Печать HTML-отчёта в PDF через Chromium (Playwright). Запуск: node tools/html2pdf.js report.html Отчёт.pdf
const { chromium } = require('playwright');
const path = require('path');
(async () => {
  const [src, out] = process.argv.slice(2);
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.goto('file://' + path.resolve(src), { waitUntil: 'load' });
  await page.pdf({ path: out, format: 'A4', printBackground: true, preferCSSPageSize: true,
    displayHeaderFooter: true, headerTemplate: '<span></span>',
    footerTemplate: '<div style="width:100%;text-align:center;font-size:9px;color:#666"><span class="pageNumber"></span></div>' });
  await browser.close();
})();
