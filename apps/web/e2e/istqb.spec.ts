import { test, expect, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

// Private source is used by the test runner; it is never bundled into the learner application.
const load = (id: string) => JSON.parse(readFileSync(resolve(__dirname, `../../../packs/${id}.json`), 'utf8'));
const pack = load('istqb-ctfl-4');

async function signUp(page: Page, tag: string) {
  await page.goto('/sign-in');
  await page.getByRole('button', { name: 'Create an account', exact: true }).click();
  await page.getByLabel('Display name').fill('ISTQB Learner');
  await page.getByLabel('Email', { exact: true }).fill(`istqb-${Date.now()}-${tag}@example.test`);
  await page.getByLabel('Password', { exact: true }).fill('BrowserTesting123');
  await page.getByRole('button', { name: 'Create account', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Welcome back, ISTQB Learner.' })).toBeVisible();
}

for (const scenario of [
  { paper: 'paper-a', minutes: 60, correct: 25, result: 'Mock fail' },
  { paper: 'paper-b-extended', minutes: 75, correct: 26, result: 'Mock pass' },
]) {
  test(`ISTQB ${scenario.paper} renders lessons, timer and ${scenario.result}`, async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', e => errors.push(e.message));
    await signUp(page, scenario.paper);
    await page.goto('/courses/istqb-ctfl-4');
    await expect(page.getByRole('heading', { name: pack.title, exact: true })).toBeVisible();
    await expect(page.getByRole('navigation', { name: 'Course lessons' }).getByRole('button')).toHaveCount(24);
    await page.getByRole('navigation', { name: 'Course lessons' }).getByRole('button').filter({ hasText: 'Black-box techniques' }).click();
    await expect(page.locator('.reading')).toContainText('Two-value BVA');
    await page.getByRole('tab', { name: 'Practice & exams' }).click();
    await page.getByLabel('Feedback mode').selectOption('mock');
    await page.getByLabel('Session length').selectOption(scenario.paper);
    await page.getByRole('button', { name: 'Begin session' }).click();
    await expect(page.locator('lf-question-input')).toBeVisible();
    const id = page.url().split('/').pop();
    const attemptResponse = await page.request.get(`/api/me/attempts/${id}`);
    const attempt = await attemptResponse.json();
    expect(attempt.questions).toHaveLength(40);
    expect(attempt.summary).toBeNull();
    expect(Date.parse(attempt.deadline) - Date.parse(attempt.startedAt)).toBe(scenario.minutes * 60000);
    expect(attempt.questions.every((q: { grading?: unknown }) => q.grading === undefined)).toBeTruthy();
    await expect(page.locator('.timer')).toContainText(scenario.minutes === 60 ? /(?:60:00|59:\d\d)/ : /(?:75:00|74:\d\d)/);

    const csrf = await (await page.request.get('/api/auth/csrf')).json();
    let revision = 0;
    for (const q of attempt.questions.slice(0, scenario.correct)) {
      const source = pack.questions.find((item: { id: string }) => item.id === q.id);
      const saved = await page.request.put(`/api/me/attempts/${id}/responses`, {
        headers: { 'X-CSRF-TOKEN': csrf.token },
        data: { revision, requestId: crypto.randomUUID(), questionId: q.id, answer: { selected: source.grading.correct, slots: {} } },
      });
      expect(saved.ok()).toBeTruthy();
      revision++;
    }
    await page.reload();
    await page.getByRole('button', { name: 'Submit session', exact: true }).click();
    await page.getByRole('button', { name: 'Confirm submission', exact: true }).click();
    await expect(page.getByRole('heading', { name: scenario.result, exact: true })).toBeVisible();
    await expect(page.getByRole('status')).toContainText(`${scenario.correct} / 40 points`);
    await expect(page.getByRole('status')).toContainText('Pass threshold: 26 points');
    await page.locator('.review-item summary').first().click();
    await expect(page.locator('.explanation').first()).toContainText('Syllabus reference:');
    await page.setViewportSize({ width: 390, height: 844 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
    await page.screenshot({ path: `test-results/istqb-${scenario.paper}-mobile.png` });
    expect(errors).toEqual([]);
  });
}

// Specialist exams weight K3 questions at two points, so answers are chosen by weight to land exactly on the pass mark.
for (const scenario of [
  { packId: 'istqb-ct-ai-2', lessons: 18, lesson: 'Calculating ML functional performance metrics', text: 'Precision = TP / (TP + FP)', passPoints: 29, possible: 44 },
  { packId: 'istqb-ct-genai-1', lessons: 16, lesson: 'Effective prompts', text: 'Prompt chaining breaks a task', passPoints: 30, possible: 46 },
]) {
  test(`ISTQB ${scenario.packId} renders lessons and scores weighted points to Mock pass`, async ({ page }) => {
    const specialist = load(scenario.packId);
    const errors: string[] = [];
    page.on('pageerror', e => errors.push(e.message));
    await signUp(page, scenario.packId);
    await page.goto(`/courses/${scenario.packId}`);
    await expect(page.getByRole('heading', { name: specialist.title, exact: true })).toBeVisible();
    await expect(page.getByRole('navigation', { name: 'Course lessons' }).getByRole('button')).toHaveCount(scenario.lessons);
    await page.getByRole('navigation', { name: 'Course lessons' }).getByRole('button').filter({ hasText: scenario.lesson }).click();
    await expect(page.locator('.reading')).toContainText(scenario.text);
    await page.getByRole('tab', { name: 'Practice & exams' }).click();
    await page.getByLabel('Feedback mode').selectOption('mock');
    await page.getByLabel('Session length').selectOption('paper-a');
    await page.getByRole('button', { name: 'Begin session' }).click();
    await expect(page.locator('lf-question-input')).toBeVisible();
    const id = page.url().split('/').pop();
    const attempt = await (await page.request.get(`/api/me/attempts/${id}`)).json();
    expect(attempt.questions).toHaveLength(40);
    expect(Date.parse(attempt.deadline) - Date.parse(attempt.startedAt)).toBe(60 * 60000);
    expect(attempt.questions.every((q: { grading?: unknown }) => q.grading === undefined)).toBeTruthy();
    await expect(page.locator('.timer')).toContainText(/(?:60:00|59:\d\d)/);
    const csrf = await (await page.request.get('/api/auth/csrf')).json();
    let revision = 0;
    let earned = 0;
    for (const q of attempt.questions) {
      const source = specialist.questions.find((item: { id: string }) => item.id === q.id);
      if (earned + source.weight > scenario.passPoints) continue;
      const saved = await page.request.put(`/api/me/attempts/${id}/responses`, {
        headers: { 'X-CSRF-TOKEN': csrf.token },
        data: { revision, requestId: crypto.randomUUID(), questionId: q.id, answer: { selected: source.grading.correct, slots: {} } },
      });
      expect(saved.ok()).toBeTruthy();
      revision++;
      earned += source.weight;
    }
    expect(earned).toBe(scenario.passPoints);
    await page.reload();
    await page.getByRole('button', { name: 'Submit session', exact: true }).click();
    await page.getByRole('button', { name: 'Confirm submission', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Mock pass', exact: true })).toBeVisible();
    await expect(page.getByRole('status')).toContainText(`${scenario.passPoints} / ${scenario.possible} points`);
    await expect(page.getByRole('status')).toContainText(`Pass threshold: ${scenario.passPoints} points`);
    await page.locator('.review-item summary').first().click();
    await expect(page.locator('.explanation').first()).toContainText('Syllabus reference:');
    await page.setViewportSize({ width: 390, height: 844 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
    expect(errors).toEqual([]);
  });
}
