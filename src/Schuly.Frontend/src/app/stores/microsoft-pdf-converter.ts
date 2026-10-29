import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { PdfConverter } from '../shared/file-viewer';
import { MicrosoftAccountsStore } from './microsoft-accounts.store';

/** Converts Office files to PDF through the user's linked Microsoft account; unavailable without one. */
@Injectable()
export class MicrosoftPdfConverter implements PdfConverter {
  private readonly http = inject(HttpClient);
  private readonly accounts = inject(MicrosoftAccountsStore);

  async available(): Promise<boolean> {
    if (!this.accounts.loaded()) await this.accounts.load();
    return this.accounts.hasAccounts();
  }

  convert(blob: Blob, name: string): Promise<Blob> {
    const id = this.accounts.selectedId();
    if (!id) return Promise.reject(new Error('no linked account'));
    return firstValueFrom(
      this.http.post(`/api/plugins/microsoft/accounts/${encodeURIComponent(id)}/convert/pdf`, blob, {
        params: { name },
        headers: { 'Content-Type': 'application/octet-stream' },
        responseType: 'blob',
      }),
    );
  }
}
