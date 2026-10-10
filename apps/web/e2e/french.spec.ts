import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

// Content check for packs/french-b2-grammar.json: typed short answers, the accent palette and accent tolerance.
interface PackQuestion {
  kind: string;
  prompt: string;
  grading: { correct: string[]; ignoreDiacritics?: boolean };
}
const pack = JSON.parse(
  readFileSync(join(process.cwd(), '..', '..', 'packs', 'french-b2-grammar.json'), 'utf8'),
) as { questions: PackQuestion[] };
const keys = new Map(
  pack.questions.filter((q) => q.kind === 'shortAnswer').map((q) => [q.prompt, q.grading]),
);
const accents = 'àâæçéèêëîïôœùûüÿäößñ';
const stripAccents = (text: string) => text.normalize('NFD').replace(/\p{Mn}/gu, '');

test('a French learner reads, types short answers with the palette, resumes and sees progress', async ({
  page,
}) => {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('/sign-in');
  await page.getByRole('button', { name: 'Create an account', exact: true }).click();
  await page.getByLabel('Display name').fill('Sasha');
  await page.getByLabel('Email', { exact: true }).fill(`french-${Date.now()}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Welcome back, Sasha.' })).toBeVisible();

  await page.goto('/courses/french-b2-grammar');
  await expect(
    page.getByRole('heading', { name: 'French B2 · Grammar rebuild', exact: true }),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Mark as complete' }).click();
  await expect(page.getByRole('button', { name: 'Lesson completed' })).toBeDisabled();
  await page
    .getByRole('navigation', { name: 'Course lessons' })
    .getByRole('button', { name: /Passé composé: être or avoir/ })
    .click();
  await expect(page.getByRole('heading', { name: 'Passé composé: être or avoir' })).toBeVisible();
  await page.getByRole('button', { name: 'Mark as complete' }).click();
  await expect(page.getByRole('button', { name: 'Lesson completed' })).toBeDisabled();

  await page.getByRole('tab', { name: 'Practice & exams' }).click();
  await page.getByLabel('Session length').selectOption('drill');
  await page.getByLabel('Feedback mode').selectOption('learn');
  await page.getByLabel('Practice focus').selectOption('objective');
  await page.getByRole('combobox', { name: /^Objective/ }).selectOption('passe-compose');
  await page.getByRole('button', { name: 'Begin session' }).click();

  const card = page.locator('.question-card');
  const saved = page.locator('.timer small');
  await expect(card.locator('lf-question-input')).toBeVisible();
  await expect(card.locator('.eyebrow')).toHaveText('QUESTION 1 / 12');
  const outcomes: string[] = [];
  for (let n = 0; n < 12 && outcomes.length < 3; n++) {
    if (n > 0) {
      await page.getByRole('button', { name: 'Next', exact: true }).click();
      await expect(card.locator('.eyebrow')).toHaveText(`QUESTION ${n + 1} / 12`);
    }
    if ((await card.locator('.pill').innerText()) !== 'Short answer') continue;
    const grading = keys.get(await card.locator('h2').innerText());
    expect(grading).toBeDefined();
    const answer = grading!.correct[0];
    const input = card.getByLabel('Your answer');

    if (outcomes.length === 0) {
      // A wrong answer is marked as such and the expected forms are shown.
      await input.fill('avons allé');
      await input.press('Enter');
      await expect(saved).toHaveText('All responses saved');
      await card.getByRole('button', { name: 'Check answer' }).click();
      await expect(page.locator('.feedback strong')).toContainText('Keep building');
      await expect(page.locator('.feedback')).toContainText(
        'Expected: ' + grading!.correct.join(' or '),
      );
      outcomes.push('wrong');
    } else if (outcomes.length === 1) {
      // Keyboard only: type up to the first accented letter, insert it from the palette, finish typing.
      const at = [...answer].findIndex((c) => accents.includes(c));
      await input.fill('');
      if (at < 0) await input.pressSequentially(answer);
      else {
        await input.pressSequentially(answer.slice(0, at));
        await page.keyboard.press('Tab');
        await expect(card.locator('summary')).toBeFocused();
        await page.keyboard.press('Enter');
        const insert = card.getByRole('button', { name: `Insert ${answer[at]}`, exact: true });
        await insert.focus();
        await page.keyboard.press('Enter');
        await expect(input).toBeFocused();
        await input.pressSequentially(answer.slice(at + 1));
      }
      await expect(input).toHaveValue(answer);
      await input.press('Enter');
      await expect(saved).toHaveText('All responses saved');
      await card.getByRole('button', { name: 'Check answer' }).click();
      await expect(page.locator('.feedback strong')).toContainText('Correct');
      outcomes.push(at < 0 ? 'typed' : 'palette');
    } else {
      // Accents are not marked in this course unless the accent changes the word.
      await input.fill(grading!.ignoreDiacritics ? stripAccents(answer).toUpperCase() : answer);
      await card.getByRole('button', { name: 'Save answer' }).click();
      await expect(saved).toHaveText('All responses saved');
      await card.getByRole('button', { name: 'Check answer' }).click();
      await expect(page.locator('.feedback strong')).toContainText('Correct');
      await expect(page.locator('.feedback')).toContainText('Expected: ' + answer);
      outcomes.push('lenient');
    }
  }
  expect(outcomes).toHaveLength(3);

  await page.reload();
  await expect(page.locator('.feedback')).toBeVisible();
  await page.getByRole('button', { name: 'Submit session', exact: true }).click();
  await page.getByRole('button', { name: 'Confirm submission', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Your results.' })).toBeVisible();
  await expect(page.locator('.review-item')).toHaveCount(12);

  await page.getByRole('link', { name: 'Overview', exact: true }).click();
  const course = page.locator('.path-panel').filter({ hasText: 'French B2 · Grammar rebuild' });
  await expect(course).toBeVisible();
  await expect(course.locator('.next-steps li').first()).toBeVisible();
  await expect(
    course.locator('lf-mastery-badge').filter({ hasNotText: 'Not started' }).first(),
  ).toBeVisible();
  expect(errors).toEqual([]);
});

test('the French course and a typed question fit a narrow screen without exposing keys', async ({
  page,
}) => {
  const catalog = await (await page.request.get('/api/catalog/french-b2-grammar')).json();
  expect(catalog.questions).toBeUndefined();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/sign-in');
  await page.getByRole('button', { name: 'Create an account', exact: true }).click();
  await page.getByLabel('Display name').fill('Mira');
  await page.getByLabel('Email', { exact: true }).fill(`french-mobile-${Date.now()}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Welcome back, Mira.' })).toBeVisible();
  await page.goto('/courses/french-b2-grammar?tab=practice');
  await page.getByLabel('Feedback mode').selectOption('learn');
  await page.getByLabel('Practice focus').selectOption('objective');
  await page.getByRole('combobox', { name: /^Objective/ }).selectOption('subjonctif-formes');
  await page.getByRole('button', { name: 'Begin session' }).click();
  const card = page.locator('.question-card');
  while ((await card.locator('.pill').innerText()) !== 'Short answer')
    await page.getByRole('button', { name: 'Next', exact: true }).click();
  await card.getByText('Accents and special characters').click();
  await expect(card.getByRole('button', { name: 'Insert é', exact: true })).toBeVisible();
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
  ).toBeTruthy();
  await page.screenshot({ path: 'test-results/french-short-answer-mobile.png', fullPage: true });
});
