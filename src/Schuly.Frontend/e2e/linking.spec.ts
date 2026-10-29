import AxeBuilder from '@axe-core/playwright';
import { Page, expect, test } from '@playwright/test';
import { ALPHA_ACCOUNT, BETA_ACCOUNT, FakeServer, doneStep, otpStep, passwordForm } from './fake-server';

let server: FakeServer;

test.beforeEach(async ({ page }) => {
  server = new FakeServer();
  await server.install(page);
});

/** Axe scan of the open dialog: WCAG A and AA rules, zero violations. */
async function expectAccessible(page: Page, selector = '[role="dialog"]') {
  const results = await new AxeBuilder({ page }).include(selector).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
  expect(results.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`)).toEqual([]);
}

async function openAddDialog(page: Page) {
  await page.goto('/settings');
  await expect(page.getByRole('heading', { name: 'Schools & accounts' })).toBeVisible();
  await page.getByRole('button', { name: 'Add account' }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
}

test('provider picker lists the providers the host offers, with descriptions', async ({ page }) => {
  await openAddDialog(page);
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('button', { name: /Alpha School/ })).toContainText('Sign in with your school account.');
  await expect(dialog.getByRole('button', { name: /Beta Cloud/ })).toContainText('Mail and files.');
  await expect(dialog.locator('img[src$="/beta/icon.svg"]')).toHaveCount(1);
  await expectAccessible(page);
});

test('form -> otp -> done: the new account is in the list and the nav the first time, no reload', async ({ page }) => {
  server.startReplies = [passwordForm];
  server.submitReplies = [otpStep, doneStep('b1', 'Mia Beta')];
  server.onDone = () => {
    server.accounts = [BETA_ACCOUNT];
  };

  await openAddDialog(page);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /Beta Cloud/ }).click();
  await expect(dialog.getByRole('radio', { name: /Password/ })).toBeChecked();
  await dialog.getByRole('button', { name: 'Continue' }).click();

  await expect(dialog.getByRole('heading', { name: 'Sign in to Beta Cloud' })).toBeVisible();
  // Focus moved into the step.
  await expect(dialog.getByLabel('Email')).toBeFocused();
  await expectAccessible(page);

  // Client-side hint, announced inline.
  await dialog.getByRole('button', { name: 'Sign in' }).click();
  await expect(dialog.getByText('Email is required.')).toBeVisible();
  await expect(dialog.getByLabel('Email')).toHaveAttribute('aria-invalid', 'true');
  await expectAccessible(page);

  await dialog.getByLabel('Email').fill('mia@beta.example');
  await dialog.getByLabel('Password').fill('hunter2-secret');
  await dialog.getByRole('button', { name: 'Sign in' }).click();

  const otp = dialog.getByLabel('Code');
  await expect(otp).toBeVisible();
  await expect(otp).toHaveAttribute('autocomplete', 'one-time-code');
  await expect(otp).toHaveAttribute('inputmode', 'numeric');
  await expect(otp).toBeFocused();
  await expectAccessible(page);

  await otp.fill('123456');
  await dialog.getByRole('button', { name: 'Verify' }).click();

  await expect(page.getByRole('dialog')).toBeHidden();
  await expect(page.getByText('Linked Mia Beta')).toBeVisible();
  // The account is in the list and in the nav right away.
  const card = page.locator('section[aria-labelledby="accounts-title"]');
  await expect(card.getByText('Mia Beta')).toBeVisible();
  await expect(card.getByText('Connected')).toBeVisible();
  await expect(page.locator('[data-sidebar="group-label"], [hlmsidebargrouplabel]').filter({ hasText: 'Beta Cloud' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Mail' })).toBeVisible();

  // What was sent: the password and the code went to the server, nowhere else, and the form was cleared.
  expect(server.submits[0]).toMatchObject({ stepId: 'credentials', values: { email: 'mia@beta.example', password: 'hunter2-secret' } });
  expect(server.submits[1]).toEqual({ stepId: 'otp', values: { code: '123456' } });
  expect(await page.evaluate(() => JSON.stringify([localStorage, sessionStorage]))).not.toContain('hunter2-secret');
  expect(server.starts[0]).toMatchObject({ providerId: 'beta', body: { platform: 'web', flowId: 'password' } });
});

test('linking the same account again shows success, not an error, and no duplicate', async ({ page }) => {
  server.accounts = [BETA_ACCOUNT];
  server.startReplies = [passwordForm];
  server.submitReplies = [doneStep('b1', 'Mia Beta')];

  await openAddDialog(page);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /Beta Cloud/ }).click();
  await dialog.getByRole('button', { name: 'Continue' }).click();
  await dialog.getByLabel('Email').fill('mia@beta.example');
  await dialog.getByLabel('Password').fill('pw');
  await dialog.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('dialog')).toBeHidden();
  await expect(page.getByText('Linked Mia Beta')).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(page.locator('section[aria-labelledby="accounts-title"]').getByText('Mia Beta')).toHaveCount(1);
});

test('a failed link shows the real server message and Try again restarts the flow', async ({ page }) => {
  server.startReplies = [passwordForm];
  server.submitReplies = [{ status: 'failed', step: { id: 'error', kind: 'error', title: 'Sign-in failed', error: "Your school's sign-in is not supported here yet." } }];

  await openAddDialog(page);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /Alpha School/ }).click();
  await dialog.getByLabel('Email').fill('mia@beta.example');
  await dialog.getByLabel('Password').fill('pw');
  await dialog.getByRole('button', { name: 'Sign in' }).click();

  await expect(dialog.getByRole('alert')).toContainText("Your school's sign-in is not supported here yet.");
  await expectAccessible(page);
  await dialog.getByRole('button', { name: 'Try again' }).click();
  await expect(dialog.getByLabel('Email')).toBeVisible();
  expect(server.starts).toHaveLength(2);
});

test('a rejected submit (429) is explained on the step and the form is still usable', async ({ page }) => {
  server.startReplies = [passwordForm];
  server.submitReplies = [{ http: 429, body: { message: 'Too many sign-in attempts. Wait 30 seconds.' } }];

  await openAddDialog(page);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /Alpha School/ }).click();
  await dialog.getByLabel('Email').fill('mia@beta.example');
  await dialog.getByLabel('Password').fill('pw');
  await dialog.getByRole('button', { name: 'Sign in' }).click();
  await expect(dialog.getByRole('alert')).toContainText('Too many sign-in attempts. Wait 30 seconds.');
  await expect(dialog.getByLabel('Email')).toHaveValue('mia@beta.example');
  await expect(dialog.getByLabel('Password')).toHaveValue('');
});

test('deviceCode: shows the code, a safe link, polls pending, pending, done', async ({ page }) => {
  server.startReplies = [
    {
      id: 'device',
      kind: 'deviceCode',
      title: 'Enter this code',
      description: 'Open the page and enter the code.',
      deviceCode: { userCode: 'ABCD-EFGH', verificationUri: 'https://login.beta.example/device', expiresAt: new Date(Date.now() + 300_000).toISOString(), pollIntervalSeconds: 1 },
    },
  ];
  server.pollReplies = [{ id: 'device', kind: 'deviceCode', deviceCode: { userCode: 'ABCD-EFGH', verificationUri: 'https://login.beta.example/device', expiresAt: new Date(Date.now() + 300_000).toISOString(), pollIntervalSeconds: 1 } }];
  let polls = 0;
  await page.route('**/api/linking/sessions/session-1', async (route) => {
    if (route.request().method() !== 'GET') return route.fallback();
    polls++;
    if (polls < 3) return route.fulfill({ json: { sessionId: 'session-1', providerId: 'beta', flowId: 'code', status: 'active', expiresAt: new Date(Date.now() + 300_000).toISOString(), step: { id: 'device', kind: 'deviceCode', deviceCode: { userCode: 'ABCD-EFGH', verificationUri: 'https://login.beta.example/device', expiresAt: new Date(Date.now() + 300_000).toISOString(), pollIntervalSeconds: 1 } } } });
    server.accounts = [BETA_ACCOUNT];
    return route.fulfill({ json: { sessionId: 'session-1', providerId: 'beta', flowId: 'code', status: 'done', expiresAt: new Date(Date.now() + 300_000).toISOString(), step: { id: 'done', kind: 'done', done: { accountId: 'b1', title: 'Mia Beta' } } } });
  });

  await openAddDialog(page);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /Beta Cloud/ }).click();
  await dialog.getByRole('radio', { name: /Code/ }).check();
  await dialog.getByRole('button', { name: 'Continue' }).click();

  await expect(dialog.getByTestId('device-code')).toHaveText('ABCD-EFGH');
  const link = dialog.getByRole('link', { name: /Open sign-in page/ });
  await expect(link).toHaveAttribute('href', 'https://login.beta.example/device');
  await expect(link).toHaveAttribute('target', '_blank');
  await expect(link).toHaveAttribute('rel', /noopener/);
  await dialog.getByRole('button', { name: 'Copy code' }).click();
  await expect(page.getByText('Code copied')).toBeVisible();
  await expectAccessible(page);

  await expect(page.getByRole('dialog')).toBeHidden({ timeout: 15_000 });
  expect(polls).toBeGreaterThanOrEqual(3);
  await expect(page.locator('section[aria-labelledby="accounts-title"]').getByText('Mia Beta')).toBeVisible();
});

test('deviceCode with a non-https verification link offers no link', async ({ page }) => {
  server.startReplies = [{ id: 'device', kind: 'deviceCode', deviceCode: { userCode: 'ZZZZ', verificationUri: 'http://evil.example/x', expiresAt: new Date(Date.now() + 300_000).toISOString(), pollIntervalSeconds: 60 } }];
  await openAddDialog(page);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /Alpha School/ }).click();
  await expect(dialog.getByTestId('device-code')).toHaveText('ZZZZ');
  await expect(dialog.getByRole('link')).toHaveCount(0);
  await expect(dialog.getByRole('alert')).toContainText('not a secure link');
});

test('pushCode: big number and countdown, then done via poll', async ({ page }) => {
  const expiresAt = new Date(Date.now() + 90_000).toISOString();
  server.startReplies = [{ id: 'push', kind: 'pushCode', title: 'Approve the sign-in', description: 'Open your authenticator app and enter this number.', pushCode: { number: '47', expiresAt, pollIntervalSeconds: 1 } }];
  server.pollReplies = [
    { id: 'push', kind: 'pushCode', pushCode: { number: '47', expiresAt, pollIntervalSeconds: 1 } },
    doneStep('b1', 'Mia Beta'),
  ];
  server.onDone = () => {
    server.accounts = [BETA_ACCOUNT];
  };

  await openAddDialog(page);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /Alpha School/ }).click();
  await expect(dialog.getByLabel('Number to approve: 47')).toHaveText('47');
  await expect(dialog.getByText(/\d:\d\d/)).toBeVisible();
  await expect(dialog.locator('[aria-live="polite"]').first()).toContainText('Your number is 4 7');
  await expectAccessible(page);

  await expect(page.getByRole('dialog')).toBeHidden({ timeout: 15_000 });
  await expect(page.locator('section[aria-labelledby="accounts-title"]').getByText('Mia Beta')).toBeVisible();
});

test('select step with a "use a code instead" action submits the action', async ({ page }) => {
  server.startReplies = [
    {
      id: 'mfa-method',
      kind: 'select',
      title: 'Choose how to verify',
      options: [{ value: 'push', label: 'Approve in the authenticator app', detail: 'Recommended' }],
      actions: [{ id: 'use-otp', label: 'Use a code instead' }],
      submitLabel: 'Continue',
    },
  ];
  server.submitReplies = [otpStep, doneStep('b1', 'Mia Beta')];
  server.onDone = () => {
    server.accounts = [BETA_ACCOUNT];
  };

  await openAddDialog(page);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /Alpha School/ }).click();
  await expect(dialog.getByRole('radio', { name: /Approve in the authenticator app/ })).toBeVisible();
  await expectAccessible(page);

  await dialog.getByRole('button', { name: 'Continue' }).click();
  await expect(dialog.getByText('Choose an option to continue.')).toBeVisible();

  await dialog.getByRole('button', { name: 'Use a code instead' }).click();
  expect(server.submits[0]).toEqual({ stepId: 'mfa-method', values: {}, action: 'use-otp' });

  await dialog.getByLabel('Code').fill('654321');
  await dialog.getByRole('button', { name: 'Verify' }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  expect(server.submits[1]).toEqual({ stepId: 'otp', values: { code: '654321' } });
});

test('select option that links another provider runs a nested flow, then refreshes the outer step', async ({ page }) => {
  const outer = { id: 'microsoft-account', kind: 'select', title: 'Choose an account', options: [{ value: 'new', label: 'Link a new account', linkProviderId: 'beta' }] };
  server.startReplies = [outer, passwordForm];
  server.submitReplies = [
    doneStep('b1', 'Mia Beta'),
    { id: 'microsoft-account', kind: 'select', title: 'Choose an account', options: [{ value: 'b1', label: 'Mia Beta' }] },
  ];
  server.onDone = () => {
    server.accounts = [BETA_ACCOUNT];
  };

  await openAddDialog(page);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /Alpha School/ }).click();
  await dialog.getByRole('radio', { name: 'Link a new account' }).check();
  await dialog.getByRole('button', { name: 'Continue' }).click();
  await dialog.getByLabel('Email').fill('mia@beta.example');
  await dialog.getByLabel('Password').fill('pw');
  await dialog.getByRole('button', { name: 'Sign in' }).click();

  await expect(dialog.getByRole('radio', { name: 'Mia Beta' })).toBeVisible();
  expect(server.submits[1]).toEqual({ stepId: 'microsoft-account', values: {}, action: 'refresh' });
  expect(server.starts.map((s) => s.providerId)).toEqual(['alpha', 'beta']);
  await expectAccessible(page);
});

test('re-authenticate starts the flow with reauthAccountId; unlink asks first and removes the account', async ({ page }) => {
  server.accounts = [{ ...BETA_ACCOUNT, status: 'expired', message: 'Sign in again to keep syncing.' }];
  server.startReplies = [passwordForm];
  server.submitReplies = [doneStep('b1', 'Mia Beta')];
  server.onDone = () => {
    server.accounts = [BETA_ACCOUNT];
  };

  await page.goto('/settings');
  const card = page.locator('section[aria-labelledby="accounts-title"]');
  await expect(card.getByText('Sign-in expired')).toBeVisible();
  await expect(card.getByText('Sign in again to keep syncing.')).toBeVisible();
  await expectAccessible(page, 'section[aria-labelledby="accounts-title"]');

  await card.getByRole('button', { name: 'Re-authenticate Mia Beta' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Email').fill('mia@beta.example');
  await dialog.getByLabel('Password').fill('pw');
  await dialog.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  expect(server.starts[0].body).toMatchObject({ reauthAccountId: 'b1', platform: 'web' });
  await expect(card.getByText('Connected')).toBeVisible();

  await card.getByRole('button', { name: 'Unlink Mia Beta' }).click();
  await expect(page.getByRole('alertdialog')).toContainText('Unlink Mia Beta?');
  await page.getByRole('alertdialog').getByRole('button', { name: 'Unlink' }).click();
  await expect(card.getByText('No schools or accounts linked yet.')).toBeVisible();
});

test('link school account -> syncing shown -> poll returns ok -> grades visible without reload; unlink -> data gone', async ({ page }) => {
  server.startReplies = [passwordForm];
  server.submitReplies = [doneStep('a1', 'Alpha Gymnasium')];
  let gets = 0;
  server.onDone = () => {
    server.accounts = [{ ...ALPHA_ACCOUNT, status: 'syncing' }];
    gets = 0;
  };
  server.onAccounts = () => {
    if (server.accounts[0]?.status !== 'syncing') return;
    // The first poll after linking still shows syncing; the second finds the sync done and the data in place.
    if (++gets >= 3) {
      server.classes = [{ id: 'c1', name: 'Mathematics' }];
      server.exams = [{ id: 'e1', name: 'Algebra test', classId: 'c1', classAverage: 4.5, date: '2025-05-05', grades: [{ id: 'g1', score: 5.5, weighting: 1 }] }];
      server.accounts = [{ ...ALPHA_ACCOUNT, status: 'ok' }];
    }
  };

  await page.goto('/grades');
  await expect(page.getByText('Algebra test')).toHaveCount(0);
  await page.goto('/settings');
  await page.getByRole('button', { name: 'Add account' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: /Alpha School/ }).click();
  await dialog.getByLabel('Email').fill('mia@beta.example');
  await dialog.getByLabel('Password').fill('pw');
  await dialog.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  await expect(page.locator('section[aria-labelledby="accounts-title"]').getByText('Syncing', { exact: true })).toBeVisible();

  // In-app navigation only: the document is never reloaded.
  await page.evaluate(() => ((window as unknown as { __marker: string }).__marker = 'still-here'));
  await page.getByRole('link', { name: 'Grades' }).click();
  await expect(page.getByTestId('syncing-notice')).toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: 'Syncing your data...' })).toBeVisible();
  await expect(page.getByText('Algebra test')).toBeVisible({ timeout: 20_000 });
  await expect(page.getByTestId('syncing-notice')).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { __marker: string }).__marker)).toBe('still-here');

  // Unlink: the data goes away at once, without a reload.
  await page.goBack();
  await page.getByRole('button', { name: 'Unlink Alpha Gymnasium' }).click();
  server.exams = [];
  server.classes = [];
  await page.getByRole('alertdialog').getByRole('button', { name: 'Unlink' }).click();
  await expect(page.getByText('No schools or accounts linked yet.')).toBeVisible();
  await page.getByRole('link', { name: 'Grades' }).click();
  await expect(page.getByText('Algebra test')).toHaveCount(0);
  expect(await page.evaluate(() => (window as unknown as { __marker: string }).__marker)).toBe('still-here');
});

test('a host-callback return (?linkSession=) reads the finished session, toasts and reloads the accounts', async ({ page }) => {
  server.pollReplies = [doneStep('b1', 'Mia Beta')];
  server.onDone = () => {
    server.accounts = [BETA_ACCOUNT];
  };
  await page.goto('/settings?linkSession=session-1');
  await expect(page.getByText('Linked Mia Beta')).toBeVisible();
  await expect(page.locator('section[aria-labelledby="accounts-title"]').getByText('Mia Beta')).toBeVisible();
  await expect(page).not.toHaveURL(/linkSession/);
});

test('a provider that failed to list its accounts shows an error row without unlink or re-authenticate', async ({ page }) => {
  server.accounts = [
    { providerId: 'beta', providerName: 'Beta Cloud', id: '', title: '', status: 'error', message: 'Beta Cloud is not answering.', capabilities: [], canReauth: false },
    ALPHA_ACCOUNT,
  ];
  await page.goto('/settings');
  const card = page.locator('section[aria-labelledby="accounts-title"]');
  await expect(card.getByText('Beta Cloud is not answering.')).toBeVisible();
  await expect(card.getByRole('button', { name: /Unlink/ })).toHaveCount(1);
  await expectAccessible(page, 'section[aria-labelledby="accounts-title"]');
});
