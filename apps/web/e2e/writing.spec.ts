import { test, expect, Page } from '@playwright/test';

const coursePath = '/courses/chinese-writing-pilot?tab=writing';
async function register(page: Page) {
  await page.goto('/sign-in');
  await page.getByRole('button', { name: 'Create an account', exact: true }).click();
  await page.getByLabel('Display name').fill('Character Learner');
  await page
    .getByLabel('Email', { exact: true })
    .fill(`writing-${Date.now()}-${Math.random().toString(36).slice(2)}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(
    page.getByRole('heading', { name: 'Welcome back, Character Learner.' }),
  ).toBeVisible();
}
async function strokePoints(page: Page) {
  const catalog = await (await page.request.get('/api/catalog/chinese-writing-pilot')).json();
  const median = catalog.characters[0].strokeData.medians[0] as number[][];
  await expect(page.locator('.writing-canvas svg g[transform]').first()).toBeVisible();
  await page.locator('.writing-canvas').scrollIntoViewIfNeeded();
  return page
    .locator('.writing-canvas svg g[transform]')
    .first()
    .evaluate((group, points) => {
      const transform = (group as SVGGraphicsElement).getScreenCTM()!;
      return points.map(([x, y]) => {
        const point = new DOMPoint(x, y).matrixTransform(transform);
        return { x: point.x, y: point.y };
      });
    }, median);
}
async function draw(page: Page, kind: 'mouse' | 'touch' | 'pen') {
  const points = await strokePoints(page);
  if (kind === 'mouse') {
    await page.mouse.move(points[0].x, points[0].y);
    await page.mouse.down();
    for (const point of points.slice(1)) await page.mouse.move(point.x, point.y, { steps: 5 });
    await page.mouse.up();
  } else {
    const cdp = await page.context().newCDPSession(page);
    if (kind === 'touch') {
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [points[0]] });
      for (const point of points.slice(1))
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [point] });
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    } else {
      await cdp.send('Input.dispatchMouseEvent', {
        type: 'mousePressed',
        ...points[0],
        button: 'left',
        buttons: 1,
        clickCount: 1,
        pointerType: 'pen',
      });
      for (const point of points.slice(1))
        await cdp.send('Input.dispatchMouseEvent', {
          type: 'mouseMoved',
          ...point,
          button: 'left',
          buttons: 1,
          pointerType: 'pen',
        });
      await cdp.send('Input.dispatchMouseEvent', {
        type: 'mouseReleased',
        ...points.at(-1)!,
        button: 'left',
        buttons: 0,
        clickCount: 1,
        pointerType: 'pen',
      });
    }
    await cdp.detach();
  }
}

test('public trainer animates, steps without motion, loads audio and prints worksheets', async ({
  page,
}) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto(coursePath);
  await expect(page.getByRole('tab', { name: 'Characters & writing' })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await expect(page.locator('.character-grid button')).toHaveCount(30);
  await page.getByRole('button', { name: 'Play stroke order', exact: true }).click();
  await page.getByLabel('Static stroke steps').check();
  await page.getByRole('button', { name: 'Stroke 1', exact: true }).click();
  await expect(page.locator('.static-model path')).toHaveCount(1);
  const audioUrl = await page.locator('audio').getAttribute('src');
  expect((await page.request.get(audioUrl!)).status()).toBe(200);
  await page.emulateMedia({ media: 'print' });
  await expect(page.locator('.writing-print')).toBeVisible();
  await expect(page.locator('.writing-print article')).toHaveCount(30);
  await page.screenshot({ path: 'test-results/writing-print.png', fullPage: true });
  await page.emulateMedia({ media: 'screen' });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect
    .poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
    .toBeTruthy();
  await page.screenshot({ path: 'test-results/writing-mobile.png', fullPage: true });
  expect(errors).toEqual([]);
});

test('mouse writing saves guided practice; recall resumes and saves independently', async ({
  page,
}) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await register(page);
  await page.goto(coursePath);
  await page.getByRole('button', { name: 'Trace', exact: true }).click();
  await page.getByRole('button', { name: 'Start saved practice', exact: true }).click();
  await draw(page, 'mouse');
  await expect(
    page.getByText(
      'Saved. Guided practice, independent recall and stroke choices are tracked separately.',
    ),
  ).toBeVisible();
  let progress = await (
    await page.request.get('/api/me/courses/chinese-writing-pilot/writing/')
  ).json();
  expect(progress.characters[0].assistedCompletions).toBe(1);
  expect(progress.characters[0].independentCompletions).toBe(0);
  await page.getByRole('button', { name: 'Recall', exact: true }).click();
  await page.getByRole('button', { name: 'Start saved practice', exact: true }).click();
  await expect(page.locator('.character-library')).not.toBeVisible();
  await page.reload();
  await page.getByRole('button', { name: 'Resume practice', exact: true }).click();
  await draw(page, 'mouse');
  await expect(
    page.getByText(
      'Saved. Guided practice, independent recall and stroke choices are tracked separately.',
    ),
  ).toBeVisible();
  progress = await (
    await page.request.get('/api/me/courses/chinese-writing-pilot/writing/')
  ).json();
  expect(progress.characters[0].independentCompletions).toBe(1);
  expect(progress.characters[0].stage).toBe(0);
  expect(errors).toEqual([]);
});

test('touch and pen input work after mobile resize, and stroke choices work by keyboard', async ({
  page,
}) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await register(page);
  await page.goto(coursePath);
  await page.setViewportSize({ width: 390, height: 844 });
  for (const kind of ['touch', 'pen'] as const) {
    await page.getByRole('button', { name: 'Trace', exact: true }).click();
    await page.getByRole('button', { name: 'Start saved practice', exact: true }).click();
    await draw(page, kind);
    await expect(
      page.getByText(
        'Saved. Guided practice, independent recall and stroke choices are tracked separately.',
      ),
    ).toBeVisible();
    await page.setViewportSize(
      kind === 'touch' ? { width: 844, height: 390 } : { width: 390, height: 844 },
    );
  }
  await page.getByRole('button', { name: 'Stroke choices', exact: true }).click();
  await page.getByRole('button', { name: 'Start saved practice', exact: true }).click();
  const option = page.getByRole('button', { name: 'Stroke shape option 1', exact: true });
  await option.focus();
  await page.keyboard.press('Enter');
  await expect(
    page.getByText(
      'Saved. Guided practice, independent recall and stroke choices are tracked separately.',
    ),
  ).toBeVisible();
  const progress = await (
    await page.request.get('/api/me/courses/chinese-writing-pilot/writing/')
  ).json();
  expect(progress.characters[0].assistedCompletions).toBe(2);
  expect(progress.characters[0].sequenceCompletions).toBe(1);
  expect(progress.characters[0].independentCompletions).toBe(0);
  expect(errors).toEqual([]);
});
