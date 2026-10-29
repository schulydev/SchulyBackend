import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LinkSessionDto, LinkStepDto, LinkingApi } from '../core/linking-api';
import { LinkSessionStore } from './link-session.store';
import { LinkedAccountsStore } from './linked-accounts.store';

const session = (step: Partial<LinkStepDto> & { id: string; kind: LinkStepDto['kind'] }, status: LinkSessionDto['status'] = 'active', id = 's1'): LinkSessionDto => ({
  sessionId: id,
  providerId: 'prov',
  flowId: 'flow',
  status,
  expiresAt: '2030-01-01T00:00:00Z',
  step,
});

describe('LinkSessionStore', () => {
  const api = { start: vi.fn(), submit: vi.fn(), poll: vi.fn(), cancel: vi.fn(() => of(undefined)) };
  const order: string[] = [];
  const accounts = {
    afterLink: vi.fn(async () => {
      order.push('afterLink');
    }),
    providers: () => [{ id: 'other', flows: [{ id: 'f1', label: 'F' }] }],
  };

  const create = () => {
    TestBed.configureTestingModule({
      providers: [LinkSessionStore, { provide: LinkingApi, useValue: api }, { provide: LinkedAccountsStore, useValue: accounts }],
    });
    return TestBed.inject(LinkSessionStore);
  };

  beforeEach(() => {
    vi.clearAllMocks();
    order.length = 0;
    TestBed.resetTestingModule();
  });

  it('reloads accounts before it reports a finished link, so the new account is listed the first time', async () => {
    api.start.mockReturnValue(of(session({ id: 'creds', kind: 'form' })));
    api.submit.mockImplementation(() => {
      order.push('submit');
      return of(session({ id: 'done', kind: 'done', done: { accountId: 'a1', title: 'Mia' } }, 'done'));
    });
    const store = create();
    await store.begin('prov');
    expect(store.phase()).toBe('active');
    await store.submit({ email: 'a@b.ch' });
    expect(order).toEqual(['submit', 'afterLink']);
    expect(store.phase()).toBe('finished');
    expect(store.linkedTitle()).toBe('Mia');
    expect(store.failure()).toBeNull();
  });

  it('treats linking the same account again as success', async () => {
    api.start.mockReturnValue(of(session({ id: 'creds', kind: 'form' })));
    api.submit.mockReturnValue(of(session({ id: 'done', kind: 'done', done: { accountId: 'existing' } }, 'done')));
    const store = create();
    await store.begin('prov');
    await store.submit({});
    expect(store.phase()).toBe('finished');
    expect(store.failure()).toBeNull();
    expect(store.error()).toBeNull();
  });

  it('shows the real server message when a flow fails', async () => {
    api.start.mockReturnValue(of(session({ id: 'x', kind: 'form' })));
    api.submit.mockReturnValue(of(session({ id: 'error', kind: 'error', error: 'Your school rejected the sign-in.' }, 'failed')));
    const store = create();
    await store.begin('prov');
    await store.submit({});
    expect(store.phase()).toBe('failed');
    expect(store.failure()).toBe('Your school rejected the sign-in.');
    expect(accounts.afterLink).not.toHaveBeenCalled();
  });

  it('keeps the step and shows a 400 message inline', async () => {
    api.start.mockReturnValue(of(session({ id: 'creds', kind: 'form' })));
    api.submit.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { message: 'Enter a valid address.' } })));
    const store = create();
    await store.begin('prov');
    await store.submit({});
    expect(store.phase()).toBe('active');
    expect(store.error()).toBe('Enter a valid address.');
    expect(accounts.afterLink).not.toHaveBeenCalled();
  });

  it('looks for the account again after a server error, since the link may have gone through', async () => {
    api.start.mockReturnValue(of(session({ id: 'creds', kind: 'form' })));
    api.submit.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500, error: null })));
    const store = create();
    await store.begin('prov');
    await store.submit({});
    expect(store.error()).toBeTruthy();
    expect(accounts.afterLink).toHaveBeenCalled();
  });

  it('shows a rate limit on the step and lets the user retry', async () => {
    api.start.mockReturnValue(of(session({ id: 'creds', kind: 'form' })));
    api.submit.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 429, error: null })));
    const store = create();
    await store.begin('prov');
    await store.submit({});
    expect(store.error()).toContain('Too many attempts');
    expect(store.busy()).toBe(false);
  });

  it('polls a code step until the server says done', async () => {
    api.start.mockReturnValue(of(session({ id: 'push', kind: 'pushCode', pushCode: { number: '42', expiresAt: '2030-01-01T00:00:00Z', pollIntervalSeconds: 2 } })));
    api.poll.mockReturnValueOnce(of(session({ id: 'push', kind: 'pushCode' }))).mockReturnValueOnce(of(session({ id: 'done', kind: 'done', done: { accountId: 'a' } }, 'done')));
    const store = create();
    await store.begin('prov');
    await store.poll();
    expect(store.phase()).toBe('active');
    await store.poll();
    expect(store.phase()).toBe('finished');
    expect(accounts.afterLink).toHaveBeenCalledTimes(1);
  });

  it('runs a nested link and then resubmits the outer step with refresh', async () => {
    api.start
      .mockReturnValueOnce(of(session({ id: 'pick', kind: 'select', options: [{ value: 'new', label: 'New', linkProviderId: 'other' }] }, 'active', 'outer')))
      .mockReturnValueOnce(of(session({ id: 'inner', kind: 'form' }, 'active', 'inner')));
    api.submit
      .mockReturnValueOnce(of(session({ id: 'done', kind: 'done', done: { accountId: 'n' } }, 'done', 'inner')))
      .mockReturnValueOnce(of(session({ id: 'pick', kind: 'select', options: [{ value: 'n', label: 'N' }] }, 'active', 'outer')));
    const store = create();
    await store.begin('prov');
    await store.startNested('other');
    expect(store.nested()).toBe(true);
    await store.submit({});
    expect(api.submit).toHaveBeenLastCalledWith('outer', { stepId: 'pick', values: {}, action: 'refresh' });
    expect(store.phase()).toBe('active');
    expect(store.nested()).toBe(false);
    expect(store.step()?.options?.[0].value).toBe('n');
    expect(accounts.afterLink).toHaveBeenCalled();
  });

  it('cancel deletes the session on the server', async () => {
    api.start.mockReturnValue(of(session({ id: 'creds', kind: 'form' })));
    const store = create();
    await store.begin('prov');
    store.cancel();
    expect(api.cancel).toHaveBeenCalledWith('s1');
    expect(store.phase()).toBe('idle');
  });

  it('restart starts the same flow again after a failure', async () => {
    api.start.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 500, error: { message: 'Down.' } }))).mockReturnValueOnce(of(session({ id: 'creds', kind: 'form' })));
    const store = create();
    await store.begin('prov', 'flow');
    expect(store.failure()).toBe('Down.');
    await store.restart();
    expect(api.start).toHaveBeenLastCalledWith('prov', { platform: 'web', flowId: 'flow' });
    expect(store.phase()).toBe('active');
  });
});
