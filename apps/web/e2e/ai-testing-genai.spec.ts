import { test, expect } from '@playwright/test';
import fs from 'node:fs';

const pack = JSON.parse(fs.readFileSync('../../packs/ai-testing-genai-practical.json', 'utf8'));

test('practical AI course supports saved reading, capstone review and graded mastery', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  const catalog = await (await page.request.get(`/api/catalog/${pack.id}`)).json();
  expect(catalog.questions).toBeUndefined();
  expect(JSON.stringify(catalog)).not.toContain('grading');
  expect(catalog.capabilities).toEqual({ lessons: true, practice: true, assessments: false });
  expect(catalog.blueprints[0].count).toBe(16);

  await page.goto('/sign-in');
  await page.getByRole('button', { name: 'Create an account', exact: true }).click();
  await page.getByLabel('Display name').fill('Sam');
  await page.getByLabel('Email', { exact: true }).fill(`practical-ai-${crypto.randomUUID()}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Welcome back, Sam.' })).toBeVisible();

  await page.goto('/courses');
  await page.getByLabel('Search your library').fill('Practical QA');
  await page.getByRole('link').filter({ has: page.getByRole('heading', { name: pack.title, exact: true }) }).click();
  await page.getByRole('button', { name: 'Add to my courses' }).click();
  await expect(page.getByRole('button', { name: 'Archive course' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Course lessons' }).getByRole('button')).toHaveCount(16);
  await page.getByRole('button', { name: 'Mark as complete' }).click();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Lesson completed' })).toBeDisabled();

  await page.setViewportSize({ width: 390, height: 844 });
  const nav = page.getByRole('navigation', { name: 'Course lessons' });
  await nav.getByRole('button').filter({ hasText: '05 · Metrics and subgroup performance' }).click();
  await expect(page.locator('.reading')).toContainText('Optional Python example');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
  await nav.getByRole('button').filter({ hasText: '16 · Capstone' }).click();
  await expect(page.locator('.reading')).toContainText('ShopHelp');
  await expect(page.locator('.reading')).toContainText('Worked solution');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();

  await page.getByRole('tab', { name: 'Practice', exact: true }).click();
  await expect(page.getByLabel('Feedback mode').locator('option')).toHaveCount(1);
  await page.getByLabel('Practice focus').selectOption('objective');
  await page.getByRole('combobox', { name: 'Objective', exact: true }).selectOption('qa-metrics');
  await page.getByRole('button', { name: 'Begin session' }).click();
  await expect(page.locator('lf-question-input')).toBeVisible();
  const attemptId = page.url().split('/').pop();
  const attempt = await (await page.request.get(`/api/me/attempts/${attemptId}`)).json();
  expect(attempt.questions).toHaveLength(5);
  expect(attempt.mode).toBe('learn');
  for (const question of attempt.questions) {
    expect(question.grading).toBeUndefined();
    expect(question.explanation).toBeUndefined();
  }

  for (let index = 0; index < attempt.questions.length; index++) {
    const source = pack.questions.find((q: { id: string }) => q.id === attempt.questions[index].id);
    const card = page.locator('.question-card');
    if (source.kind === 'numeric') {
      await card.getByLabel('Your answer').fill(source.grading.correct[0]);
      await card.getByRole('button', { name: 'Save answer', exact: true }).click();
    } else {
      const option = source.options.find((o: { id: string }) => o.id === source.grading.correct[0]);
      await card.getByRole('radio', { name: option.text }).check();
    }
    await expect(page.locator('.timer small')).toHaveText('All responses saved');
    await card.getByRole('button', { name: 'Check answer', exact: true }).click();
    await expect(page.locator('.feedback')).toContainText(source.explanation);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
    if (index < attempt.questions.length - 1) await page.getByRole('button', { name: 'Next →', exact: true }).click();
  }
  await page.getByRole('button', { name: 'Submit session', exact: true }).click();
  await page.getByRole('button', { name: 'Confirm submission', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Your results.' })).toBeVisible();
  const progress = await (await page.request.get(`/api/me/courses/${pack.id}`)).json();
  const metrics = progress.objectives.find((o: { id: string }) => o.id === 'qa-metrics');
  expect(metrics.state).toBe('proficient');
  expect(metrics.considered).toBe(5);
  expect(progress.completedLessons).toContain('orientation');
  expect(errors).toEqual([]);
});
