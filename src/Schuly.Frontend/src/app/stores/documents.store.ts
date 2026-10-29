import { HttpClient } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { StudentDocumentDto, StudentDocumentsService } from '../api';
import { withCollection } from './with-collection';

export const DocumentsStore = signalStore(
  withCollection<StudentDocumentDto>(() => {
    const api = inject(StudentDocumentsService);
    return () => api.apiDocumentsGet();
  }),
  withState<{ downloading: string[]; downloadError: string | null }>({ downloading: [], downloadError: null }),
  withComputed((store) => ({
    sorted: computed(() => [...store.items()].sort((a, b) => (b.createdAt ?? '').localeCompare(a.createdAt ?? ''))),
  })),
  withMethods((store, http = inject(HttpClient)) => ({
    isDownloading(id: string | null | undefined): boolean {
      return !!id && store.downloading().includes(id);
    },
    download(document: StudentDocumentDto): void {
      const id = document.id;
      if (!id || store.downloading().includes(id)) return;
      patchState(store, { downloading: [...store.downloading(), id], downloadError: null });
      const done = () => patchState(store, { downloading: store.downloading().filter((d) => d !== id) });
      // Raw HttpClient with responseType blob: the generated client asks for JSON here, so it fails to parse the file bytes.
      http.get(`/api/documents/${encodeURIComponent(id)}`, { responseType: 'blob' }).subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const link = Object.assign(window.document.createElement('a'), { href: url, download: document.fileName ?? document.title ?? 'document' });
          link.click();
          // Revoking right after click() cancels the download in some browsers.
          setTimeout(() => URL.revokeObjectURL(url), 60_000);
          done();
        },
        error: () => {
          patchState(store, { downloadError: `Could not download ${document.title ?? 'the document'}. Try again.` });
          done();
        },
      });
    },
  })),
);
