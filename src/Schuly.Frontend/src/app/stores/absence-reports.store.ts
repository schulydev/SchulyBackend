import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, untracked } from '@angular/core';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { AbsencesStore } from './absences.store';
import { CapabilityAccount, LinkedAccountsStore } from './linked-accounts.store';

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

const base = (account: CapabilityAccount) => `${account.basePath}/${encodeURIComponent(account.id)}/absences`;

export function absenceErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) return fallback;
  const body = error.error as { code?: string; message?: string } | null;
  if (typeof body?.message === 'string' && body.message.trim()) return body.message.trim();
  switch (body?.code) {
    case 'school_rejected':
      return body.message || 'Your school did not accept this report.';
    case 'not_withdrawable':
      return 'Your school already processed this report, so it can no longer be withdrawn.';
    case 'absence_reporting_not_allowed':
      return 'Your school does not let you report absences here. Contact your school instead.';
    case 'signin_expired':
      return 'Your school sign-in expired. Re-authenticate it in Settings > Schools & accounts, then try again.';
    case 'school_unavailable':
      return 'Your school could not be reached right now. Try again in a moment.';
  }
  if (error.status === 502) return 'Your school could not be reached right now. Try again in a moment.';
  if (error.status === 400) return body?.message || 'Check the details and try again.';
  return fallback;
}

export const AbsenceReportsStore = signalStore(
  withState<AbsenceReportsState>({ reports: [], loading: false, unavailable: false, reasons: {} }),
  withComputed((store, linked = inject(LinkedAccountsStore)) => ({
    accounts: computed(() => linked.capabilityAccounts().absence_reporting),
    available: computed(() => !store.unavailable() && linked.capabilityAccounts().absence_reporting.length > 0),
  })),
  withMethods((store, http = inject(HttpClient), linked = inject(LinkedAccountsStore)) => ({
    async load(): Promise<void> {
      const accounts = linked.capabilityAccounts().absence_reporting;
      patchState(store, { loading: true });
      let missing = 0;
      const groups = await Promise.all(
        accounts.map(async (account) => {
          try {
            const rows = await firstValueFrom(
              http.get<ReportedAbsenceDto[]>(`${base(account)}/reported`),
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
      const account = linked.capabilityAccounts().absence_reporting.find((a) => a.id === accountId);
      if (!account) return [];
      const cached = store.reasons()[accountId];
      if (cached) return cached;
      try {
        const rows = await firstValueFrom(http.get<string[]>(`${base(account)}/reasons`));
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
      const account = linked.capabilityAccounts().absence_reporting.find((a) => a.id === accountId);
      if (!account) throw new Error('This account is not linked any more.');
      await firstValueFrom(http.post(base(account), request));
    },
    async withdraw(report: ReportedAbsence): Promise<void> {
      const account = linked.capabilityAccounts().absence_reporting.find((a) => a.id === report.accountId);
      if (!account) throw new Error('This account is not linked any more.');
      await firstValueFrom(
        http.delete(base(account), { params: { ids: report.ids.join(',') } }),
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
      const linked = inject(LinkedAccountsStore);
      effect(() => {
        linked.capabilityAccounts().absence_reporting;
        untracked(() => void store.load());
      });
    },
  }),
);
