import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { LinkSessionDto, LinkingApi } from '../core/linking-api';
import { failureMessage, isTerminal, linkErrorMessage } from '../core/linking-step';
import { LinkedAccountsStore } from './linked-accounts.store';

export type LinkPhase = 'idle' | 'starting' | 'active' | 'submitting' | 'finished' | 'failed';

interface LinkSessionState {
  phase: LinkPhase;
  session: LinkSessionDto | null;
  /** The session waiting while a nested link (another provider) runs. */
  outer: LinkSessionDto | null;
  /** Non-terminal problem shown on the current step (a rejected submit, a rate limit). */
  error: string | null;
  /** Why the flow ended without linking; the real server message. */
  failure: string | null;
  /** Title of the account that was linked. */
  linkedTitle: string | null;
  /** Bumped on every finished link so the dialog can react once. */
  finishedCount: number;
  start: { providerId: string; flowId: string | null; reauthAccountId: string | null } | null;
}

const initial: LinkSessionState = {
  phase: 'idle',
  session: null,
  outer: null,
  error: null,
  failure: null,
  linkedTitle: null,
  finishedCount: 0,
  start: null,
};

/**
 * Drives one linking flow: start, submit, poll, nested links and cancel. Provided by the dialog, so every open starts clean.
 * After any successful link it reloads the accounts and everything derived from them before reporting `finished`,
 * so the new account is in the list the first time.
 */
export const LinkSessionStore = signalStore(
  withState<LinkSessionState>(initial),
  withComputed((store) => ({
    step: computed(() => store.session()?.step ?? null),
    busy: computed(() => store.phase() === 'starting' || store.phase() === 'submitting'),
    nested: computed(() => store.outer() !== null),
  })),
  withMethods((store, api = inject(LinkingApi), accounts = inject(LinkedAccountsStore)) => {
    let polling = false;

    const drop = (session: LinkSessionDto | null) => {
      if (session) void firstValueFrom(api.cancel(session.sessionId)).catch(() => undefined);
    };

    /** The server may or may not have linked the account when a request failed in transit or on its side; look. */
    const maybeLinked = (error: unknown) => {
      const status = error instanceof HttpErrorResponse ? error.status : 0;
      if (status === 0 || status === 409 || status >= 500) void accounts.afterLink();
    };

    const finish = async (session: LinkSessionDto) => {
      await accounts.afterLink();
      patchState(store, { phase: 'finished', session, error: null, failure: null, linkedTitle: session.step.done?.title ?? null, finishedCount: store.finishedCount() + 1 });
    };

    const resumeOuter = async () => {
      const outer = store.outer();
      if (!outer) return;
      try {
        const next = await firstValueFrom(api.submit(outer.sessionId, { stepId: outer.step.id, values: {}, action: 'refresh' }));
        patchState(store, { outer: null });
        await apply(next);
      } catch (error) {
        patchState(store, { outer: null, session: outer, phase: 'active', error: linkErrorMessage(error) });
      }
    };

    const apply = async (session: LinkSessionDto): Promise<void> => {
      const end = isTerminal(session);
      if (end === 'done') {
        if (store.outer()) {
          // The nested provider linked its account: tell the outer step to look again.
          await accounts.afterLink();
          await resumeOuter();
          return;
        }
        return finish(session);
      }
      if (end === 'failed') {
        patchState(store, { phase: 'failed', session, error: null, failure: failureMessage(session) });
        return;
      }
      patchState(store, { phase: 'active', session, error: null, failure: null });
    };

    const begin = async (providerId: string, flowId: string | null = null, reauthAccountId: string | null = null): Promise<void> => {
      drop(store.session());
      drop(store.outer());
      patchState(store, { ...initial, phase: 'starting', start: { providerId, flowId, reauthAccountId } });
      try {
        const session = await firstValueFrom(
          api.start(providerId, { platform: 'web', ...(flowId ? { flowId } : {}), ...(reauthAccountId ? { reauthAccountId } : {}) }),
        );
        await apply(session);
      } catch (error) {
        patchState(store, { phase: 'failed', failure: linkErrorMessage(error, 'Could not start the sign-in. Try again.') });
        maybeLinked(error);
      }
    };

    return {
      begin,

      async submit(values: Record<string, string>, action?: string): Promise<void> {
        const session = store.session();
        if (!session || store.busy()) return;
        patchState(store, { phase: 'submitting', error: null });
        try {
          const next = await firstValueFrom(api.submit(session.sessionId, { stepId: session.step.id, values, ...(action ? { action } : {}) }));
          await apply(next);
        } catch (error) {
          patchState(store, { phase: 'active', error: linkErrorMessage(error) });
          maybeLinked(error);
        }
      },

      /** One poll of a pushCode / deviceCode step; overlapping calls are dropped. */
      async poll(): Promise<void> {
        const session = store.session();
        if (!session || polling || store.phase() !== 'active') return;
        polling = true;
        try {
          const next = await firstValueFrom(api.poll(session.sessionId));
          if (store.session()?.sessionId === session.sessionId) await apply(next);
        } catch (error) {
          if (error instanceof HttpErrorResponse && error.status === 404) {
            patchState(store, { phase: 'failed', failure: linkErrorMessage(error) });
          } else {
            patchState(store, { error: linkErrorMessage(error) });
          }
          maybeLinked(error);
        } finally {
          polling = false;
        }
      },

      /** A select option that links another provider first: run that flow, then refresh this step. */
      async startNested(providerId: string): Promise<void> {
        const outer = store.session();
        if (!outer || store.busy()) return;
        patchState(store, { phase: 'starting', error: null, outer });
        try {
          const providers = accounts.providers().find((p) => p.id === providerId);
          const session = await firstValueFrom(api.start(providerId, { platform: 'web', ...(providers?.flows[0] ? { flowId: providers.flows[0].id } : {}) }));
          await apply(session);
        } catch (error) {
          patchState(store, { outer: null, session: outer, phase: 'active', error: linkErrorMessage(error, 'Could not start the sign-in. Try again.') });
        }
      },

      /** Back out of a nested link, or cancel the whole flow. */
      cancel(): void {
        const outer = store.outer();
        if (outer) {
          drop(store.session());
          patchState(store, { session: outer, outer: null, phase: 'active', error: null });
          return;
        }
        drop(store.session());
        patchState(store, { ...initial, finishedCount: store.finishedCount() });
      },

      /** Try again after a terminal failure: start the same flow over. */
      async restart(): Promise<void> {
        const start = store.start();
        if (!start) return;
        await begin(start.providerId, start.flowId, start.reauthAccountId);
      },
    };
  }),
  withHooks((store, api = inject(LinkingApi)) => ({
    onDestroy: () => {
      // Leaving the dialog while a session is open ends it on the server too.
      for (const session of [store.session(), store.outer()]) {
        if (session && store.phase() !== 'finished') void firstValueFrom(api.cancel(session.sessionId)).catch(() => undefined);
      }
    },
  })),
);
