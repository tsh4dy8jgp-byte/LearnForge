import { test, expect } from '@playwright/test';

test('flashcards review resumes, exports, and keeps recall separate from mastery', async ({
  page,
}) => {
  await page.goto('/sign-in');
  await page.getByRole('button', { name: 'Create an account', exact: true }).click();
  await page.getByLabel('Display name').fill('Recall Learner');
  await page.getByLabel('Email', { exact: true }).fill(`recall-${Date.now()}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Welcome back, Recall Learner.' })).toBeVisible();

  // The API seeds packs/ at startup, which includes the recall-foundations sample.
  await page.goto('/courses/recall-foundations?tab=flashcards');
  await expect(page.getByRole('tab', { name: 'Flashcards' })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await page.getByRole('button', { name: 'Start spaced review', exact: true }).click();
  const review = page.getByRole('region', { name: 'Flashcard review' });
  await expect(review.getByRole('button', { name: 'Reveal answer', exact: true })).toBeVisible();
  await review.getByRole('button', { name: 'Reveal answer', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(review.getByRole('heading', { name: 'Answer', exact: true })).toBeFocused();
  await expect(review.getByRole('group', { name: 'How well did you recall it?' })).toBeVisible();
  await page.reload();
  await page.getByRole('button', { name: 'Resume review', exact: true }).click();
  await expect(review.getByRole('button', { name: 'Good', exact: true })).toBeVisible();
  await review.getByRole('button', { name: 'Good', exact: true }).click();
  await expect(review.getByRole('status')).toHaveText('Card 2 of 9');
  const progress = await (await page.request.get('/api/me/courses/recall-foundations')).json();
  expect(progress.flashcards.introduced).toBe(1);
  expect(
    progress.objectives.every((o: { state: string }) => o.state === 'notStarted'),
  ).toBeTruthy();
  await review.getByRole('button', { name: 'Finish this session', exact: true }).click();
  await expect(review.getByRole('status')).toHaveText('Review finished');
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Download Anki deck', exact: true }).click();
  expect((await download).suggestedFilename()).toMatch(/\.apkg$/);
  const pdf = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Printable PDF', exact: true }).click();
  expect((await pdf).suggestedFilename()).toMatch(/\.pdf$/);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect
    .poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
    .toBeTruthy();
  await page.screenshot({ path: 'test-results/flashcards-mobile.png', fullPage: true });
});

test('public flashcard references remain readable and text backup downloads', async ({ page }) => {
  await page.goto('/courses/recall-foundations?tab=flashcards');
  const card = page.locator('.card-list details').first();
  await card.locator('summary').click();
  await expect(card.getByText('Recall the answer from memory.', { exact: true })).toBeVisible();
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Text backup', exact: true }).click();
  expect((await download).suggestedFilename()).toMatch(/\.zip$/);
});
