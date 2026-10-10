/* Layout review checks only. No Unity, player saves or measured car outputs. */
const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const root = path.resolve(__dirname, '..');
const out = path.join(root, 'reports/phases/4a');
const media = path.join(out, 'local');
fs.mkdirSync(media, { recursive: true });
const failures = [], errors = [];
let checks = 0;
function check(value, message) { checks++; if (!value) failures.push(message); }
(async () => {
  const browser = await chromium.launch({ headless: true, channel: process.env.DRAGLAB_REVIEW_BROWSER || 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
    page.on('pageerror', e => errors.push(e.message));
    page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
    await page.goto(pathToFileURL(path.join(root, 'docs/ui/4a/index.html')).href);
    const screens = await page.locator('#screen-picker option').evaluateAll(xs => xs.map(x => x.value));
    check(screens.length === 16, 'Sixteen review screens present');
    const states = ['healthy','worn','incomplete','reserved','empty'];
    for (const width of [1440,1280,960,800,640]) {
      await page.setViewportSize({ width, height: 1000 });
      for (const screen of screens) {
        await page.selectOption('#screen-picker', screen);
        for (const state of states) {
          await page.selectOption('#state-picker', state);
          const geometry = await page.evaluate(() => {
            const main = document.getElementById('screen');
            return { title: !!main.querySelector('h1'), bodyWidth: document.body.scrollWidth, viewport: innerWidth, mainWidth: main.getBoundingClientRect().width, overflow: main.scrollWidth-main.clientWidth, notes: document.querySelectorAll('#annotations h3').length };
          });
          check(geometry.title && geometry.mainWidth > 0, `${width}/${screen}/${state}: title and nonzero layout`);
          check(geometry.bodyWidth <= geometry.viewport + 2 && geometry.overflow <= 2, `${width}/${screen}/${state}: horizontal overflow (${JSON.stringify(geometry)})`);
          check(geometry.notes === 4, `${width}/${screen}/${state}: phase + three annotations`);
        }
      }
    }
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.selectOption('#state-picker', 'healthy');
    await page.selectOption('#screen-picker', 'tuning');
    await page.locator('[aria-label="Launch RPM"]').fill('4200');
    check(await page.locator('.page-title .badge').innerText() === 'UNAPPLIED DRAFT', 'Tuning draft label reacts');
    await page.locator('[data-revert]').click();
    check(await page.locator('.page-title .badge').innerText() === 'Saved values', 'Revert restores mock saved state');
    await page.selectOption('#screen-picker', 'shop');
    await page.locator('[data-add]').first().click();
    check((await page.locator('#cart-count').innerText()).startsWith('3 '), 'Cart demo interaction reacts');
    await page.locator('[data-confirm="cart"]').click();
    check(await page.locator('#review-dialog').evaluate(e => e.open), 'Trade dialog opens');
    await page.locator('#close-dialog').click();
    await page.selectOption('#screen-picker', 'settings');
    await page.locator('[data-settings-tab="Simulation"]').click();
    await page.locator('#heat-managed').uncheck();
    check(await page.locator('#start-temps').isDisabled(), 'Continuous optimum disables starting-temperature choice');
    await page.locator('#heat-managed').check();
    check(await page.locator('#start-temps').isEnabled(), 'Heat management restores starting-temperature choice');
    for (const screen of ['sound','physical']) {
      await page.selectOption('#screen-picker', screen);
      check(await page.locator('[aria-label="Throttle"]').isDisabled(), `${screen}: keyboard feedback is read-only`);
      await page.locator('[data-bench-tab="'+(screen==='sound'?'Induction':'Parts')+'"]').click();
      check(await page.locator('#screen select').count() > 0, `${screen}: config remains editable`);
      await page.locator('[data-bench-tab="Drive"]').click();
      await page.locator('[data-owner="Sliders"]').click();
      check(await page.locator('[aria-label="Throttle"]').isEnabled(), `${screen}: explicit slider takeover`);
      await page.locator('[data-owner="Keyboard"]').click();
      for (const tab of await page.locator('[data-bench-tab]').evaluateAll(xs => xs.map(x => x.dataset.benchTab))) {
        await page.locator(`[data-bench-tab="${tab}"]`).click();
        check((await page.locator('#screen').innerText()).length > 200, `${screen}/${tab}: populated layout`);
      }
    }
    await page.selectOption('#screen-picker', 'coverage');
    const all = await page.locator('#coverage-count').innerText();
    check(all.startsWith('1051 '), 'UI, reflected fields and keyboard templates in explorer');
    await page.locator('#coverage-search').fill('twoStep');
    check(!(await page.locator('#coverage-count').innerText()).startsWith('0 '), 'Source field search returns two-step controls');
    await page.locator('#annotations-toggle').click();
    check(await page.locator('#annotations').evaluate(e => e.hidden), 'Annotations hide');
    await page.locator('#annotations-toggle').click();
    check(!(await page.locator('#annotations').evaluate(e => e.hidden)), 'Annotations restore');
    for (const [screen,state,width] of [['garage','healthy',1440],['shop','healthy',1440],['tuning','healthy',1440],['maintenance','worn',1440],['settings','healthy',1280],['race','healthy',1280],['sound','healthy',1440],['physical','healthy',1440],['garage','empty',960],['shop','reserved',960],['tuning','incomplete',800]]) {
      await page.setViewportSize({ width, height: 1000 });
      await page.selectOption('#screen-picker', screen);
      await page.selectOption('#state-picker', state);
      if (screen === 'settings') { await page.locator('[data-settings-tab="Simulation"]').click(); }
      await page.screenshot({ path: path.join(media, `${screen}-${state}-${width}.png`), fullPage: true });
    }
    check(errors.length === 0, 'No browser console/runtime errors: '+errors.join('; '));
    const lines = ['4A layout review verification', `Checks: ${checks}`, `Failures: ${failures.length}`, `Browser errors: ${errors.length}`, '16 screens x 5 states x 5 widths (1440/1280/960/800/640).', 'Interactions: draft/revert, cart/dialog, heat dependency, bench ownership/config/tabs, coverage search and annotations.', '11 local screenshots retained; values are illustrative, not Unity/physics validation.', ...failures];
    fs.writeFileSync(path.join(out, 'layout-verification.txt'), lines.join('\n')+'\n', 'utf8');
    console.log(lines.slice(0,7).join('\n'));
    if (failures.length) { console.error(failures.slice(0,12).join('\n')); process.exitCode = 1; }
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
