import { expect, test, type Page } from '@playwright/test';

const layouts = { sidebar: 'Workspace', header: 'Campus', focus: 'Focus' } as const;
const schemes = { brand: 'Site brand', ocean: 'Ocean', plum: 'Plum', terracotta: 'Terracotta', midnight: 'Midnight' } as const;
const storageKey = 'learnforge.appearance.v1';

async function choose(page: Page, name: string) {
  const radio = page.getByRole('radio', { name, exact: true });
  await page.locator('label').filter({ has: radio }).click();
  await expect(radio).toBeChecked();
}

async function expectFits(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
}

// Resolve the actual CSS tokens in the browser, including color-mix values.
async function paletteContrast(page: Page) {
  return page.evaluate(() => {
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = 1;
    const context = canvas.getContext('2d')!;
    const probe = document.createElement('span');
    document.body.append(probe);
    function luminance(token: string) {
      probe.style.color = `var(${token})`;
      context.fillStyle = getComputedStyle(probe).color;
      context.fillRect(0, 0, 1, 1);
      const rgb = [...context.getImageData(0, 0, 1, 1).data].slice(0, 3).map(value => {
        const c = value / 255;
        return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
      });
      return rgb[0] * 0.2126 + rgb[1] * 0.7152 + rgb[2] * 0.0722;
    }
    const pairs = [
      ['--ink', '--paper'], ['--ink', '--white'], ['--ink', '--lime'],
      ['--muted', '--paper'], ['--muted', '--white'], ['--muted', '--subtle'],
      ['--green', '--paper'], ['--green', '--white'], ['--green', '--lime'],
      ['--on-primary', '--green'], ['--on-danger', '--danger-solid'],
      ['--success-text', '--success-bg'], ['--warning-text', '--warning-bg'], ['--danger-text', '--danger-bg'],
    ];
    const result = pairs.map(([foreground, background]) => {
      const a = luminance(foreground); const b = luminance(background);
      return { pair: `${foreground} on ${background}`, ratio: (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05) };
    });
    probe.remove();
    return result;
  });
}

for (const [layout, name] of Object.entries(layouts)) {
  for (const width of [320, 768, 1440]) {
    test(`${name} supports every palette at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 1000 });
      await page.goto('/appearance');
      await choose(page, name);
      await expect(page.locator('html')).toHaveAttribute('data-layout', layout);
      for (const [scheme, schemeName] of Object.entries(schemes)) {
        await choose(page, schemeName);
        await expect(page.locator('html')).toHaveAttribute('data-color-scheme', scheme);
        await expectFits(page);
        for (const { pair, ratio } of await paletteContrast(page)) {
          expect(ratio, `${scheme}: ${pair}`).toBeGreaterThanOrEqual(4.5);
        }
        const previewScheme = { sidebar: 'brand', header: 'ocean', focus: 'midnight' }[layout];
        if (width === 1440 && scheme === previewScheme) {
          await page.evaluate(() => scrollTo(0, 0));
          await page.screenshot({ path: `test-results/appearance-${layout}.png`, fullPage: true });
        }
      }
      await page.getByRole('link', { name: 'Learning library' }).click();
      await expect(page.locator('.course-card').first()).toBeVisible();
      await expectFits(page);
      await page.goto('/courses/reasoning-foundations');
      await expect(page.locator('.reading')).toBeVisible();
      await expectFits(page);
      await expect(page.locator('.profile').getByRole('link', { name: 'Sign in' })).toBeVisible();
      await page.locator('.profile').getByRole('link', { name: 'Sign in' }).click();
      await expect(page.getByLabel('Email', { exact: true })).toBeVisible();
      await expectFits(page);
      if (width === 320) await page.screenshot({ path: `test-results/sign-in-${layout}-mobile.png`, fullPage: true });
    });
  }
}

test('keyboard choices survive reload and reset follows the configured site defaults', async ({ page }) => {
  await page.route('**/api/site-settings', async route => {
    const response = await route.fetch();
    await route.fulfill({ json: { ...await response.json(), layout: 'header', colorScheme: 'plum', primaryColor: '#6b3d72' } });
  });
  await page.goto('/appearance');
  await expect(page.getByRole('radio', { name: 'Campus', exact: true })).toBeChecked();
  await expect(page.getByRole('radio', { name: 'Plum', exact: true })).toBeChecked();
  await page.getByRole('radio', { name: 'Campus', exact: true }).focus();
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('radio', { name: 'Focus', exact: true })).toBeChecked();
  await choose(page, 'Midnight');
  await page.reload();
  await expect(page.getByRole('radio', { name: 'Focus', exact: true })).toBeChecked();
  await expect(page.getByRole('radio', { name: 'Midnight', exact: true })).toBeChecked();
  await page.getByRole('button', { name: 'Use site defaults' }).click();
  await expect(page.getByRole('radio', { name: 'Campus', exact: true })).toBeChecked();
  await expect(page.getByRole('radio', { name: 'Plum', exact: true })).toBeChecked();
  expect(await page.evaluate(key => localStorage.getItem(key), storageKey)).toBeNull();
  await choose(page, 'Site brand');
  expect(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--green').trim())).toBe('#6b3d72');
  await choose(page, 'Ocean');
  expect(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--green').trim())).toBe('#1e5d86');
});

test('a learner uses dark Focus, saves an answer, and resumes in Campus without losing progress', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/appearance');
  await choose(page, 'Focus');
  await choose(page, 'Midnight');
  await page.locator('.profile').getByRole('link', { name: 'Sign in' }).click();
  await page.getByRole('button', { name: 'Create an account', exact: true }).click();
  await page.getByLabel('Display name').fill('Appearance learner');
  await page.getByLabel('Email', { exact: true }).fill(`appearance-${Date.now()}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Welcome back, Appearance learner.' })).toBeVisible();
  await expectFits(page);
  await page.goto('/courses/reasoning-foundations');
  await page.getByRole('tab', { name: 'Practice & exams' }).click();
  await page.getByLabel('Feedback mode').selectOption('learn');
  await page.getByRole('button', { name: 'Begin session' }).click();
  await expect(page.locator('lf-question-input')).toBeVisible();
  // Find a single-choice question without depending on shuffled delivery order.
  for (let n = 0; n < 5; n++) {
    if (await page.locator('.question-card input[type=radio]').count()) break;
    await page.getByRole('button', { name: 'Next →', exact: true }).click();
    await expect(page.locator('.question-card .eyebrow')).toHaveText(`QUESTION ${n + 2} / 5`);
  }
  const answer = page.locator('.question-card input[type=radio]').first();
  await answer.check();
  await expect(page.locator('.timer small')).toHaveText('All responses saved');
  const question = await page.locator('.question-card .eyebrow').innerText();
  const questionNumber = Number(question.match(/\d+/)?.[0]);
  const attemptUrl = page.url();
  await page.getByRole('link', { name: 'Appearance', exact: true }).click();
  await choose(page, 'Campus');
  await choose(page, 'Terracotta');
  await page.goto(attemptUrl);
  // The server preserves the answer; the learner can select the answered question again.
  await page.getByRole('button', { name: `Question ${questionNumber}, answered`, exact: true }).click();
  await expect(page.locator('.question-card .eyebrow')).toHaveText(question);
  await expect(page.locator('.question-card input[type=radio]').first()).toBeChecked();
  await expect(page.locator('html')).toHaveAttribute('data-layout', 'header');
  await expect(page.locator('html')).toHaveAttribute('data-color-scheme', 'terracotta');
  await page.setViewportSize({ width: 390, height: 844 });
  await expectFits(page);
  await page.getByRole('button', { name: 'Check answer', exact: true }).click();
  await expect(page.locator('.feedback')).toBeVisible();
  await expect(page.locator('.profile').getByRole('button', { name: 'Sign out' })).toBeVisible();
  expect(errors).toEqual([]);
});
