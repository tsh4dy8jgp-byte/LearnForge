import { expect, test } from '@playwright/test';

const path = '/courses/chinese-writing-pilot';

test('public study previews support word lookup, keyboard controls and mobile reflow', async ({
  page,
}) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto(path);
  await expect(page.getByRole('tab', { name: "Today's study", exact: true })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await expect(page.locator('.contrast-card')).toHaveCount(5);
  await expect(page.locator('.reading-card')).toHaveCount(6);
  await page.screenshot({ path: 'test-results/character-study-desktop.png', fullPage: true });
  await page
    .locator('.reading-card')
    .filter({ hasText: 'Introduce a student' })
    .getByRole('button', { name: 'Preview with help' })
    .click();
  await expect(
    page.getByRole('heading', { name: 'Introduce a student', exact: true }),
  ).toBeFocused();
  await expect(page.locator('.translation')).toHaveCount(0);
  const word = page.locator('.reading-word').first();
  await word.focus();
  await page.keyboard.press('Enter');
  await expect(word).toHaveAttribute('aria-expanded', 'true');
  await expect(page.locator('.word-help')).toContainText('wǒ');
  await page.getByRole('button', { name: 'Show translation', exact: true }).click();
  await expect(page.getByText('I am a student.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Show pronunciation', exact: true }).click();
  await expect(page.locator('.pinyin')).toHaveCount(6);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect
    .poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth))
    .toBeTruthy();
  await page.screenshot({
    path: 'test-results/character-study-reading-mobile.png',
    fullPage: true,
  });
  await page.getByRole('button', { name: 'Finish reading', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Reading complete', exact: true })).toBeFocused();
  expect(errors).toEqual([]);
});

test('daily study resumes, saves five reviews, schedules mistakes and unlocks a reading', async ({
  page,
}) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/sign-in');
  await page.getByRole('button', { name: 'Create an account', exact: true }).click();
  await page.getByLabel('Display name').fill('Daily Learner');
  await page.getByLabel('Email', { exact: true }).fill(`daily-${Date.now()}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Welcome back, Daily Learner.' })).toBeVisible();
  await page.goto(path);
  await page.getByRole('button', { name: "Start today's study", exact: true }).click();
  await expect(page.getByText('Card 1 of 3', { exact: true })).toBeVisible();
  await page.reload();
  await page.getByRole('button', { name: 'Resume saved review', exact: true }).click();
  await expect(page.getByText('Card 1 of 3', { exact: true })).toBeVisible();
  for (let i = 0; i < 3; i++) {
    await page.getByRole('button', { name: 'Reveal answer', exact: true }).click();
    await page.getByRole('button', { name: 'Good', exact: true }).click();
  }
  await page.getByRole('button', { name: 'Continue to comparisons', exact: true }).click();
  await expect(page.getByText('Card 1 of 2', { exact: true })).toBeVisible();
  const catalog = await (await page.request.get('/api/catalog/chinese-writing-pilot')).json();
  for (let i = 0; i < 2; i++) {
    const summary = await (
      await page.request.get('/api/me/courses/chinese-writing-pilot/flashcards/')
    ).json();
    const session = await (
      await page.request.get(
        `/api/me/courses/chinese-writing-pilot/flashcards/sessions/${summary.activeSessionId}`,
      )
    ).json();
    const pair = catalog.characterStudy.contrasts.find((p: { noteIds: string[] }) =>
      p.noteIds.includes(session.card.noteId),
    );
    const index = pair.noteIds.indexOf(session.card.noteId);
    const characterId = pair.characterIds[i === 0 ? 1 - index : index];
    const glyph = catalog.characters.find((c: { id: string }) => c.id === characterId).glyph;
    await page.locator('.character-choice').filter({ hasText: glyph }).click();
    await expect(
      page.getByRole('heading', {
        name: i === 0 ? 'Look at the difference' : 'You spotted it',
        exact: true,
      }),
    ).toBeFocused();
    if (i === 0)
      await page.screenshot({
        path: 'test-results/character-study-comparison.png',
        fullPage: true,
      });
    await page.getByRole('button', { name: 'Save and continue', exact: true }).click();
    if (i === 0) await expect(page.getByText('Card 2 of 2', { exact: true })).toBeVisible();
  }
  await page.getByRole('button', { name: 'Continue to reading', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Count to three', exact: true })).toBeVisible();
  await expect(page.getByText('Preview with help:', { exact: false })).toHaveCount(0);
  await page.getByRole('button', { name: 'Finish reading', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Study complete', exact: true })).toBeFocused();
  // The visit began with a resumed session, so all five ratings were saved in this visit.
  await expect(
    page.getByText('5 review ratings were saved during this visit.', { exact: false }),
  ).toBeVisible();
  const exported = await (await page.request.get('/api/me/export')).json();
  expect(exported.evidence).toEqual([]);
  expect(exported.flashcards.schedules).toHaveLength(5);
  expect(
    exported.flashcards.events.filter((e: { operation: string }) => e.operation === 'rate'),
  ).toHaveLength(5);
  expect(exported.flashcards.events.some((e: { rating: string }) => e.rating === 'again')).toBe(
    true,
  );
  expect(errors).toEqual([]);
});
