import { computed, inject } from '@angular/core';
import { signalStore, withComputed, withMethods } from '@ngrx/signals';
import { StudentDocumentDto, StudentDocumentsService } from '../api';
import { withCollection } from './with-collection';

export const DocumentsStore = signalStore(
  { providedIn: 'root' },
  withCollection<StudentDocumentDto>(() => {
    const api = inject(StudentDocumentsService);
    return () => api.apiDocumentsGet();
  }),
  withComputed((store) => ({
    sorted: computed(() => [...store.items()].sort((a, b) => (b.createdAt ?? '').localeCompare(a.createdAt ?? ''))),
  })),
  withMethods((_, api = inject(StudentDocumentsService)) => ({
    download(document: StudentDocumentDto): void {
      if (!document.id) return;
      api.apiDocumentsDocumentIdGet(document.id, 'body', false, { httpHeaderAccept: undefined }).subscribe((blob: Blob) => {
        const url = URL.createObjectURL(blob);
        const link = Object.assign(window.document.createElement('a'), { href: url, download: document.fileName ?? document.title });
        link.click();
        URL.revokeObjectURL(url);
      });
    },
  })),
);
