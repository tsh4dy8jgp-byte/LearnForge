import { test, expect } from '@playwright/test';

test('a learner registers, reads, uses every question format, resumes and reviews', async ({
  page,
}) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/sign-in');
  await page.getByRole('button', { name: 'Create an account', exact: true }).click();
  await page.getByLabel('Display name').fill('Alex');
  await page.getByLabel('Email', { exact: true }).fill(`browser-${Date.now()}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Welcome back, Alex.' })).toBeVisible();
  await page.getByRole('link', { name: 'Explore your courses' }).click();
  await page
    .getByRole('link')
    .filter({ has: page.getByRole('heading', { name: 'Reasoning foundations' }) })
    .click();
  await page.getByRole('button', { name: 'Mark as complete' }).click();
  await expect(page.getByRole('button', { name: 'Lesson completed' })).toBeDisabled();
  await page.getByRole('tab', { name: 'Content map' }).click();
  await expect(page.getByRole('heading', { name: 'How the ideas connect' })).toBeVisible();
  await page.getByRole('tab', { name: 'Practice & exams' }).click();
  await page.getByLabel('Feedback mode').selectOption('learn');
  await page.getByRole('button', { name: 'Begin session' }).click();
  await expect(page.locator('lf-question-input')).toBeVisible();
  const seen = new Set<string>();
  for (let n = 0; n < 5; n++) {
    const card = page.locator('.question-card');
    const kind = await card.locator('.pill').innerText();
    seen.add(kind);
    if (kind === 'Single choice') await card.locator('input[type=radio]').first().check();
    if (kind === 'Select multiple') {
      await card.locator('input[type=checkbox]').nth(0).check();
      await expect(page.locator('.timer small')).toHaveText('All responses saved');
      await card.locator('input[type=checkbox]').nth(1).check();
    }
    if (kind === 'Sequence') await card.getByRole('button', { name: 'Save this order' }).click();
    if (kind === 'Dropdown blanks') {
      for (const select of await card.locator('select').all()) {
        await select.selectOption({ index: 1 });
        await expect(page.locator('.timer small')).toHaveText('All responses saved');
      }
    }
    if (kind === 'Drag & match') {
      for (const target of await card.locator('.match-target').all()) {
        await card.locator('.token-bank button').first().click();
        await target.getByRole('button', { name: 'Place token here' }).click();
        await expect(page.locator('.timer small')).toHaveText('All responses saved');
      }
    }
    await expect(page.locator('.timer small')).toHaveText('All responses saved');
    await card.getByRole('button', { name: 'Check answer' }).click();
    await expect(page.locator('.feedback')).toBeVisible();
    if (n < 4) {
      await page.getByRole('button', { name: 'Next →', exact: true }).click();
      await expect(card.locator('.eyebrow')).toHaveText(`QUESTION ${n + 2} / 5`);
    }
  }
  expect(seen.size).toBe(5);
  await page.reload();
  await expect(page.locator('.feedback')).toBeVisible();
  await page.getByRole('button', { name: 'Submit session', exact: true }).click();
  await page.getByRole('button', { name: 'Confirm submission', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Your results.' })).toBeVisible();
  await expect(page.locator('.review-item')).toHaveCount(5);
  await page.locator('.review-item summary').first().click();
  await expect(page.locator('.explanation').first()).toBeVisible();
  await page.getByRole('link', { name: 'Attempts & results', exact: false }).first().click();
  await expect(page.getByText('Learning session', { exact: true })).toBeVisible();
  expect(errors).toEqual([]);
});

test('public course views fit a narrow screen and never include answer keys', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const response = await page.request.get('/api/catalog/reasoning-foundations');
  const body = await response.json();
  expect(body.questions).toBeUndefined();
  await page.goto('/courses/reasoning-foundations');
  await expect(
    page.getByRole('heading', { name: 'Reasoning foundations', exact: true }),
  ).toBeVisible();
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
  ).toBeTruthy();
  await page.screenshot({ path: 'test-results/course-mobile.png', fullPage: true });
});
