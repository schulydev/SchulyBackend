import { Component, inject, signal } from '@angular/core';
import { form, FormField, FormRoot, validate } from '@angular/forms/signals';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideSend } from '@ng-icons/lucide';
import { toast } from '@spartan-ng/brain/sonner';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmDialogImports } from '@spartan-ng/helm/dialog';
import { HlmFieldImports } from '@spartan-ng/helm/field';
import { HlmInput } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmSpinner } from '@spartan-ng/helm/spinner';
import { HlmSwitch } from '@spartan-ng/helm/switch';
import { HlmTextarea } from '@spartan-ng/helm/textarea';
import { startOfToday, toIsoDate } from '../core/dates';
import {
  AbsenceReportRequest,
  AbsenceReportsStore,
  absenceErrorMessage,
} from '../stores/absence-reports.store';

const MAX_LENGTH = 500;

interface ReportModel {
  dateFrom: string;
  dateTo: string;
  partial: boolean;
  timeFrom: string;
  timeTo: string;
  reason: string;
  comment: string;
}

const emptyModel = (): ReportModel => {
  const today = toIsoDate(startOfToday());
  return {
    dateFrom: today,
    dateTo: today,
    partial: false,
    timeFrom: '',
    timeTo: '',
    reason: '',
    comment: '',
  };
};

@Component({
  selector: 'app-absence-report-dialog',
  imports: [
    FormRoot,
    FormField,
    NgIcon,
    HlmAlertImports,
    HlmButton,
    HlmDialogImports,
    HlmFieldImports,
    HlmInput,
    HlmNativeSelectImports,
    HlmSpinner,
    HlmSwitch,
    HlmTextarea,
  ],
  providers: [provideIcons({ lucideSend })],
  template: `
    <button hlmBtn type="button" (click)="show()">
      <ng-icon name="lucideSend" aria-hidden="true" />
      Report absence
    </button>

    <hlm-dialog [state]="open() ? 'open' : 'closed'" (closed)="open.set(false)">
      <hlm-dialog-content
        *hlmDialogPortal="let ctx"
        class="max-h-[90dvh] overflow-y-auto sm:max-w-lg"
      >
        <hlm-dialog-header>
          <h2 hlmDialogTitle>Report an absence</h2>
          <p hlmDialogDescription>
            This is sent to your school (Schulnetz) and appears for each of your classes. You can
            withdraw it until the school has processed it.
          </p>
        </hlm-dialog-header>

        <form [formRoot]="report" class="flex flex-col gap-4" [attr.aria-busy]="submitting()">
          <fieldset class="flex flex-col gap-4" [disabled]="submitting()">
            @if (reports.accounts().length > 1) {
              <div hlmField>
                <label hlmFieldLabel for="absence-account">School</label>
                <hlm-native-select
                  selectId="absence-account"
                  [value]="accountId()"
                  (valueChange)="accountId.set($event ?? '')"
                >
                  @for (account of reports.accounts(); track account.id) {
                    <option hlmNativeSelectOption [value]="account.id">{{ account.title }}</option>
                  }
                </hlm-native-select>
              </div>
            }

            <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div hlmField>
                <label hlmFieldLabel for="absence-from">From</label>
                <input
                  hlmInput
                  id="absence-from"
                  type="date"
                  [formField]="report.dateFrom"
                  (change)="fromChanged()"
                />
                @if (report.dateFrom().touched() && report.dateFrom().invalid()) {
                  <hlm-field-error>{{ report.dateFrom().errors()[0]?.message }}</hlm-field-error>
                }
              </div>
              <div hlmField>
                <label hlmFieldLabel for="absence-to">To</label>
                <input hlmInput id="absence-to" type="date" [formField]="report.dateTo" />
                @if (report.dateTo().touched() && report.dateTo().invalid()) {
                  <hlm-field-error>{{ report.dateTo().errors()[0]?.message }}</hlm-field-error>
                }
              </div>
            </div>

            <div class="flex items-center gap-2">
              <hlm-switch
                inputId="absence-partial"
                [checked]="model().partial"
                (checkedChange)="setPartial($event)"
              />
              <label for="absence-partial" class="text-sm">Only some lessons</label>
            </div>

            @if (model().partial) {
              <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div hlmField>
                  <label hlmFieldLabel for="absence-time-from">From time</label>
                  <input
                    hlmInput
                    id="absence-time-from"
                    type="time"
                    [formField]="report.timeFrom"
                  />
                  @if (report.timeFrom().touched() && report.timeFrom().invalid()) {
                    <hlm-field-error>{{ report.timeFrom().errors()[0]?.message }}</hlm-field-error>
                  }
                </div>
                <div hlmField>
                  <label hlmFieldLabel for="absence-time-to">To time</label>
                  <input hlmInput id="absence-time-to" type="time" [formField]="report.timeTo" />
                  @if (report.timeTo().touched() && report.timeTo().invalid()) {
                    <hlm-field-error>{{ report.timeTo().errors()[0]?.message }}</hlm-field-error>
                  }
                </div>
              </div>
            }

            <div hlmField>
              <label hlmFieldLabel for="absence-reason">Reason (optional)</label>
              <input
                hlmInput
                id="absence-reason"
                autocomplete="off"
                placeholder="Illness"
                [formField]="report.reason"
              />
              @if (report.reason().touched() && report.reason().invalid()) {
                <hlm-field-error>{{ report.reason().errors()[0]?.message }}</hlm-field-error>
              }
            </div>

            <div hlmField>
              <label hlmFieldLabel for="absence-comment">Comment (optional)</label>
              <textarea
                hlmTextarea
                id="absence-comment"
                rows="3"
                [formField]="report.comment"
              ></textarea>
              @if (report.comment().touched() && report.comment().invalid()) {
                <hlm-field-error>{{ report.comment().errors()[0]?.message }}</hlm-field-error>
              }
            </div>
          </fieldset>

          @if (error()) {
            <div hlmAlert variant="destructive" role="alert">
              <p hlmAlertDescription>{{ error() }}</p>
            </div>
          }

          <hlm-dialog-footer>
            <button
              hlmBtn
              variant="outline"
              type="button"
              [disabled]="submitting()"
              (click)="open.set(false)"
            >
              Cancel
            </button>
            <button hlmBtn type="submit" [disabled]="submitting()" [attr.aria-busy]="submitting()">
              @if (submitting()) {
                <hlm-spinner aria-hidden="true" />
              }
              Send report
            </button>
          </hlm-dialog-footer>
        </form>
      </hlm-dialog-content>
    </hlm-dialog>
  `,
})
export class AbsenceReportDialog {
  protected readonly reports = inject(AbsenceReportsStore);

  protected readonly open = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly accountId = signal('');

  protected readonly model = signal<ReportModel>(emptyModel());
  private lastFrom = this.model().dateFrom;

  protected readonly report = form(
    this.model,
    (path) => {
      validate(path.dateFrom, ({ value }) =>
        value() ? undefined : { kind: 'required', message: 'Choose a start date.' },
      );
      validate(path.dateTo, ({ value, valueOf }) => {
        if (!value()) return { kind: 'required', message: 'Choose an end date.' };
        const from = valueOf(path.dateFrom);
        return from && value() < from
          ? { kind: 'range', message: 'The end date cannot be before the start date.' }
          : undefined;
      });
      validate(path.timeFrom, ({ valueOf }) =>
        valueOf(path.partial) && !valueOf(path.timeFrom)
          ? { kind: 'required', message: 'Choose a start time.' }
          : undefined,
      );
      validate(path.timeTo, ({ valueOf }) => {
        if (!valueOf(path.partial)) return undefined;
        const to = valueOf(path.timeTo);
        if (!to) return { kind: 'required', message: 'Choose an end time.' };
        const sameDay = valueOf(path.dateFrom) === valueOf(path.dateTo);
        return sameDay && valueOf(path.timeFrom) && to <= valueOf(path.timeFrom)
          ? { kind: 'range', message: 'The end time must be after the start time.' }
          : undefined;
      });
      validate(path.reason, ({ value }) =>
        value().length > MAX_LENGTH
          ? { kind: 'maxLength', message: `Use ${MAX_LENGTH} characters or fewer.` }
          : undefined,
      );
      validate(path.comment, ({ value }) =>
        value().length > MAX_LENGTH
          ? { kind: 'maxLength', message: `Use ${MAX_LENGTH} characters or fewer.` }
          : undefined,
      );
    },
    { submission: { action: () => this.send() } },
  );

  protected submitting(): boolean {
    return this.report().submitting();
  }

  protected show(): void {
    this.model.set(emptyModel());
    this.lastFrom = this.model().dateFrom;
    this.error.set(null);
    this.accountId.set(this.reports.accounts()[0]?.id ?? '');
    this.report().reset();
    this.open.set(true);
  }

  protected fromChanged(): void {
    const { dateFrom, dateTo } = this.model();
    if (!dateTo || dateTo === this.lastFrom || dateTo < dateFrom) {
      this.model.update((m) => ({ ...m, dateTo: dateFrom }));
    }
    this.lastFrom = dateFrom;
  }

  protected setPartial(partial: boolean): void {
    this.model.update((m) => ({ ...m, partial }));
  }

  private async send(): Promise<undefined> {
    this.error.set(null);
    const m = this.model();
    const request: AbsenceReportRequest = { dateFrom: m.dateFrom, dateTo: m.dateTo };
    if (m.partial) {
      request.hourFrom = m.timeFrom;
      request.hourTo = m.timeTo;
    }
    if (m.reason.trim()) request.reason = m.reason.trim();
    if (m.comment.trim()) request.comment = m.comment.trim();
    try {
      await this.reports.report(this.accountId() || this.reports.accounts()[0].id, request);
    } catch (error) {
      this.error.set(absenceErrorMessage(error, 'Could not send the report. Try again.'));
      return undefined;
    }
    this.open.set(false);
    toast.success('Absence reported to your school.');
    await this.reports.refresh();
    return undefined;
  }
}
