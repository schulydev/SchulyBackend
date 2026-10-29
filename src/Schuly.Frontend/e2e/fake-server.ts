import { Page, Route } from '@playwright/test';

/* In-memory stand-in for the Schuly API: auth, linking and the school data the linked accounts unlock. */

export interface Capability {
  kind: string;
  label: string;
  icon?: string;
  basePath?: string;
}

export interface FakeAccount {
  providerId: string;
  providerName: string;
  id: string;
  title: string;
  detail?: string;
  status: 'ok' | 'expired' | 'error' | 'syncing';
  message?: string;
  capabilities: Capability[];
  canReauth: boolean;
}

export interface Step {
  id: string;
  kind: string;
  [key: string]: unknown;
}

export type Reply = Step | { status: 'done' | 'failed' | 'expired'; step: Step } | { http: number; body?: unknown };

export const PROVIDERS = [
  {
    id: 'alpha',
    displayName: 'Alpha School',
    description: 'Sign in with your school account.',
    icon: { lucide: 'graduation-cap' },
    capabilities: [{ kind: 'absence_reporting', label: 'Absences', basePath: '/api/alpha/accounts' }],
    flows: [{ id: 'main', label: 'School sign-in' }],
    sortOrder: 0,
  },
  {
    id: 'beta',
    displayName: 'Beta Cloud',
    description: 'Mail and files.',
    icon: { svgUrl: '/api/linking/providers/beta/icon.svg' },
    capabilities: [{ kind: 'mail', label: 'Mail', basePath: '/api/beta/accounts' }],
    flows: [
      { id: 'password', label: 'Password', description: 'Email and password' },
      { id: 'code', label: 'Code', description: 'A code from your phone' },
    ],
    sortOrder: 1,
  },
];

export const BETA_ACCOUNT: FakeAccount = {
  providerId: 'beta',
  providerName: 'Beta Cloud',
  id: 'b1',
  title: 'Mia Beta',
  detail: 'mia@beta.example',
  status: 'ok',
  capabilities: [{ kind: 'mail', label: 'Mail', basePath: '/api/beta/accounts' }],
  canReauth: true,
};

export const ALPHA_ACCOUNT: FakeAccount = {
  providerId: 'alpha',
  providerName: 'Alpha School',
  id: 'a1',
  title: 'Alpha Gymnasium',
  detail: 'alpha.example',
  status: 'ok',
  capabilities: [
    { kind: 'absence_reporting', label: 'Absences', basePath: '/api/alpha/accounts' },
    { kind: 'school_data', label: 'School data' },
  ],
  canReauth: true,
};

export class FakeServer {
  accounts: FakeAccount[] = [];
  /** Exams the school-data endpoints return; tests fill it when an account finishes syncing. */
  exams: unknown[] = [];
  classes: unknown[] = [];
  /** Replies for the next calls, per route kind. Each call consumes one; the last one repeats. */
  startReplies: Reply[] = [];
  submitReplies: Reply[] = [];
  pollReplies: Reply[] = [];
  accountsGets = 0;
  submits: { stepId: string; values: Record<string, string>; action?: string }[] = [];
  starts: { providerId: string; body: Record<string, unknown> }[] = [];
  cancelled: string[] = [];
  /** Called for each GET /api/linking/accounts so tests can flip a status over time. */
  onAccounts: (() => void) | null = null;
  /** Runs when a submit reply is a done session, before the reply is sent: the server has linked the account. */
  onDone: (() => void) | null = null;

  private next(replies: Reply[]): Reply {
    return replies.length > 1 ? replies.shift()! : replies[0];
  }

  private session(reply: Reply, id: string, providerId = 'beta') {
    if ('http' in reply) return null;
    const base = { sessionId: id, providerId, flowId: 'password', expiresAt: new Date(Date.now() + 600_000).toISOString() };
    if ('step' in reply) return { ...base, status: reply.status, step: reply.step };
    return { ...base, status: 'active', step: reply };
  }

  private respond(route: Route, reply: Reply, id: string, providerId?: string) {
    if ('http' in reply) return route.fulfill({ status: reply.http, contentType: 'application/json', body: JSON.stringify(reply.body ?? {}) });
    const session = this.session(reply, id, providerId)!;
    if (session.status === 'done') this.onDone?.();
    return route.fulfill({ json: session });
  }

  async install(page: Page): Promise<void> {
    await page.addInitScript(() => {
      // Keep the clipboard writes of the copy button from needing a permission prompt.
      Object.defineProperty(navigator, 'clipboard', { value: { writeText: () => Promise.resolve() }, configurable: true });
    });

    // Everything else under /api and /auth answers with an empty list so unrelated pages do not fail.
    await page.route(
      (url) => url.pathname.startsWith('/api/') || url.pathname.startsWith('/auth/'),
      (route) => route.fulfill({ json: [] }),
    );
    await page.route('**/auth/refresh', (route) => route.fulfill({ json: { access_token: 'test-token', expires_in: 900, token_type: 'Bearer' } }));
    await page.route(/\/api\/Auth\/me$/i, (route) => route.fulfill({ json: { id: 'u1', externalId: 'u1', email: 'mia@example.org', displayName: 'Mia', schoolUsers: [] } }));
    await page.route(/\/api\/Exams$/i, (route) => route.fulfill({ json: this.exams }));
    await page.route(/\/api\/Class$/i, (route) => route.fulfill({ json: this.classes }));

    await page.route(/\/api\/linking\/providers(\?.*)?$/, (route) => route.fulfill({ json: PROVIDERS }));
    await page.route('**/api/linking/providers/*/icon.svg', (route) =>
      route.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" fill="#1d4ed8"/></svg>' }),
    );
    await page.route('**/api/linking/accounts', (route) => {
      this.accountsGets++;
      this.onAccounts?.();
      return route.fulfill({ json: this.accounts });
    });
    await page.route('**/api/linking/accounts/*/*', (route) => {
      const [, , , , , providerId, id] = new URL(route.request().url()).pathname.split('/');
      this.accounts = this.accounts.filter((a) => !(a.providerId === providerId && a.id === id));
      return route.fulfill({ status: 204 });
    });
    await page.route('**/api/linking/providers/*/start', (route) => {
      const providerId = new URL(route.request().url()).pathname.split('/')[4];
      this.starts.push({ providerId, body: route.request().postDataJSON() });
      return this.respond(route, this.next(this.startReplies), 'session-1', providerId);
    });
    await page.route('**/api/linking/sessions/*/submit', (route) => {
      this.submits.push(route.request().postDataJSON());
      return this.respond(route, this.next(this.submitReplies), 'session-1');
    });
    await page.route('**/api/linking/sessions/*', (route) => {
      const method = route.request().method();
      if (method === 'DELETE') {
        this.cancelled.push(new URL(route.request().url()).pathname.split('/').pop()!);
        return route.fulfill({ status: 204 });
      }
      return this.respond(route, this.next(this.pollReplies), 'session-1');
    });
  }
}

export const doneStep = (accountId: string, title: string): Reply => ({ status: 'done', step: { id: 'done', kind: 'done', done: { accountId, title } } });

export const passwordForm: Step = {
  id: 'credentials',
  kind: 'form',
  title: 'Sign in to Beta Cloud',
  description: 'Use your work or school account.',
  fields: [
    { key: 'email', label: 'Email', type: 'email', required: true, placeholder: 'you@school.example' },
    { key: 'password', label: 'Password', type: 'password', required: true, secret: true },
  ],
  submitLabel: 'Sign in',
};

export const otpStep: Step = { id: 'otp', kind: 'otp', title: 'Enter your code', description: 'From your authenticator app.', otp: { length: 6, label: 'Code', numericOnly: true } };
