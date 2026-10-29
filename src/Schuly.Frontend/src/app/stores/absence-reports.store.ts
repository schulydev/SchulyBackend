import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, untracked } from '@angular/core';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { AbsencesStore } from './absences.store';
import { ConnectionsStore } from './connections.store';

export interface AbsenceReportRequest {
  dateFrom: string;
  dateTo: string;
  hourFrom?: string;
  hourTo?: string;
  reason?: string;
  comment?: string;
}

interface ReportedAbsenceDto {
  ids: string[];
  dateFrom: string;
  dateTo: string;
  hourFrom: string | null;
  hourTo: string | null;
  reason: string | null;
  comment: string | null;
  profiles: string[];
  reportedAt: string | null;
}

export interface ReportedAbsence extends ReportedAbsenceDto {
  accountId: string;
  accountTitle: string;
}

interface AbsenceReportsState {
  reports: ReportedAbsence[];
  loading: boolean;
  unavailable: boolean;
  reasons: Record<string, string[]>;
}

const base = (accountId: string) =>
  `/api/plugins/schulware/accounts/${encodeURIComponent(accountId)}/absences`;

export function absenceErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) return fallback;
  const body = error.error as { code?: string; message?: string } | null;
  switch (body?.code) {
    case 'schulnetz_rejected':
      return body.message || 'Your school did not accept this report.';
    case 'not_withdrawable':
      return 'Your school already processed this report, so it can no longer be withdrawn.';
    case 'absence_reporting_not_allowed':
      return 'Your school does not let you report absences here. Contact your school instead.';
    case 'schulnetz_signin_expired':
      return 'Your school sign-in expired. Reconnect the school in the Schuly mobile app, then try again.';
    case 'schulnetz_unavailable':
      return 'Your school could not be reached right now. Try again in a moment.';
  }
  if (error.status === 502) return 'Your school could not be reached right now. Try again in a moment.';
  if (error.status === 400) return body?.message || 'Check the details and try again.';
  return fallback;
}

export const AbsenceReportsStore = signalStore(
  withState<AbsenceReportsState>({ reports: [], loading: false, unavailable: false, reasons: {} }),
  withComputed((store, connections = inject(ConnectionsStore)) => ({
    accounts: connections.schulnetzAccounts,
    available: computed(() => !store.unavailable() && connections.schulnetzAccounts().length > 0),
  })),
  withMethods((store, http = inject(HttpClient), connections = inject(ConnectionsStore)) => ({
    async load(): Promise<void> {
      const accounts = connections.schulnetzAccounts();
      patchState(store, { loading: true });
      let missing = 0;
      const groups = await Promise.all(
        accounts.map(async (account) => {
          try {
            const rows = await firstValueFrom(
              http.get<ReportedAbsenceDto[]>(`${base(account.id)}/reported`),
            );
            return (Array.isArray(rows) ? rows : []).map((row) => ({
              ...row,
              ids: row.ids ?? [],
              profiles: row.profiles ?? [],
              accountId: account.id,
              accountTitle: account.title,
            }));
          } catch (error) {
            if (error instanceof HttpErrorResponse && error.status === 404) missing++;
            return [];
          }
        }),
      );
      patchState(store, {
        reports: groups.flat().sort((a, b) => b.dateFrom.localeCompare(a.dateFrom)),
        unavailable: accounts.length > 0 && missing === accounts.length,
        loading: false,
      });
    },
    /** Reasons the school offers, cached per account; any failure yields an empty list (not cached). */
    async loadReasons(accountId: string): Promise<string[]> {
      const cached = store.reasons()[accountId];
      if (cached) return cached;
      try {
        const rows = await firstValueFrom(http.get<string[]>(`${base(accountId)}/reasons`));
        const reasons = Array.isArray(rows)
          ? rows.filter((r): r is string => typeof r === 'string' && r.trim() !== '')
          : [];
        patchState(store, (s) => ({ reasons: { ...s.reasons, [accountId]: reasons } }));
        return reasons;
      } catch {
        return [];
      }
    },
    async report(accountId: string, request: AbsenceReportRequest): Promise<void> {
      await firstValueFrom(http.post(base(accountId), request));
    },
    async withdraw(report: ReportedAbsence): Promise<void> {
      await firstValueFrom(
        http.delete(base(report.accountId), { params: { ids: report.ids.join(',') } }),
      );
    },
  })),
  withMethods((store, absences = inject(AbsencesStore)) => ({
    async refresh(): Promise<void> {
      absences.load();
      await store.load();
    },
  })),
  withHooks({
    onInit(store) {
      const connections = inject(ConnectionsStore);
      effect(() => {
        connections.schulnetzAccounts();
        untracked(() => void store.load());
      });
    },
  }),
);
