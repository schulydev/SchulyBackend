import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods } from '@ngrx/signals';
import { cleanPreview } from '../core/preview-text';
import { MicrosoftAccountsStore } from './microsoft-accounts.store';
import { withMicrosoftResource } from './microsoft-resource';

/** Microsoft Graph message, as returned by SchulyMicrosoft GET /api/mail (outnote `emails`). */
export interface MailMessage {
  id: string;
  subject?: string | null;
  bodyPreview?: string | null;
  receivedDateTime?: string | null;
  isRead?: boolean | null;
  webLink?: string | null;
  hasAttachments?: boolean | null;
  flag?: { flagStatus?: string | null } | null;
  toRecipients?: { emailAddress?: { name?: string | null; address?: string | null } | null }[] | null;
  from?: { emailAddress?: { name?: string | null; address?: string | null } | null } | null;
}

/** Microsoft Graph chat with expanded members and lastMessagePreview (outnote `chats`). */
export interface TeamsChat {
  id: string;
  topic?: string | null;
  chatType?: string | null;
  webUrl?: string | null;
  lastUpdatedDateTime?: string | null;
  members?: { displayName?: string | null; email?: string | null }[] | null;
  lastMessagePreview?: {
    createdDateTime?: string | null;
    isDeleted?: boolean | null;
    body?: { content?: string | null; contentType?: string | null } | null;
    from?: { user?: { displayName?: string | null } | null } | null;
  } | null;
}

/** OneNote notebook from me/onenote/notebooks (outnote `notebooks`). */
export interface OneNoteNotebook {
  id: string;
  displayName?: string | null;
  lastModifiedDateTime?: string | null;
  isShared?: boolean | null;
  links?: { oneNoteWebUrl?: { href?: string | null } | null } | null;
}

/**
 * OneDrive driveItem from me/drive/{path}/children (outnote `files`). Folders carry `folder`, files carry `file`;
 * `@microsoft.graph.downloadUrl` is a short-lived pre-authenticated link and only present on files.
 */
export interface DriveItem {
  id: string;
  name?: string | null;
  size?: number | null;
  lastModifiedDateTime?: string | null;
  webUrl?: string | null;
  folder?: { childCount?: number | null } | null;
  file?: { mimeType?: string | null } | null;
  '@microsoft.graph.downloadUrl'?: string | null;
}

export const MailStore = signalStore(
  withMicrosoftResource<MailMessage>('mail', { folder: 'inbox', unread: false }, { pageSize: 25, max: 500 }),
  withComputed((store) => ({
    messages: computed(() =>
      store.items().map((m) => ({
        id: m.id,
        sender: m.from?.emailAddress?.name || m.from?.emailAddress?.address || 'Unknown sender',
        address: m.from?.emailAddress?.address ?? null,
        subject: m.subject || '(no subject)',
        preview: cleanPreview(m.bodyPreview),
        flagged: m.flag?.flagStatus === 'flagged',
        hasAttachments: m.hasAttachments === true,
        to: (m.toRecipients ?? []).map((r) => r.emailAddress?.name || r.emailAddress?.address || '').filter(Boolean),
        received: m.receivedDateTime ?? null,
        unread: m.isRead === false,
        link: m.webLink ?? null,
      })),
    ),
    unreadOnly: computed(() => store.params()['unread'] === true),
    folder: computed(() => String(store.params()['folder'] ?? 'inbox')),
  })),
  withMethods((store) => ({
    setUnreadOnly: (unread: boolean) => store.setParam('unread', unread),
    setFolder: (folder: string) => store.setParam('folder', folder),
    /** Removes a message from the loaded list after it was deleted or moved. */
    drop: (id: string) => patchState(store, { items: store.items().filter((m) => m.id !== id) }),
    /** Applies a local change (read, flag) to a loaded message so the list matches without a reload. */
    patch: (id: string, change: Partial<MailMessage>) =>
      patchState(store, { items: store.items().map((m) => (m.id === id ? { ...m, ...change } : m)) }),
  })),
);

const plainText = (body?: { content?: string | null; contentType?: string | null } | null): string => {
  const content = body?.content ?? '';
  if (!content) return '';
  return (body?.contentType === 'html' ? htmlText(content) : content).replace(/\s+/g, ' ').trim();
};

/** Visible text of an HTML message; script and style contents are not text a reader sees. */
const htmlText = (html: string): string => {
  const body = new DOMParser().parseFromString(html, 'text/html').body;
  body.querySelectorAll('script, style').forEach((element) => element.remove());
  return body.textContent ?? '';
};

const chatKinds: Record<string, string> = { oneOnOne: 'Chat', group: 'Group', meeting: 'Meeting' };

export const ChatsStore = signalStore(
  withMicrosoftResource<TeamsChat>('chats', { top: 25 }),
  withComputed((store, accounts = inject(MicrosoftAccountsStore)) => ({
    chats: computed(() => {
      const own = accounts.selected()?.email?.toLowerCase();
      return store.items().map((c) => {
        const members = (c.members ?? []).filter((m) => m.displayName);
        const others = members.filter((m) => !own || m.email?.toLowerCase() !== own);
        const names = (others.length ? others : members).map((m) => m.displayName).join(', ');
        const last = c.lastMessagePreview;
        const preview = last?.isDeleted ? '' : plainText(last?.body);
        const sender = last?.from?.user?.displayName;
        return {
          id: c.id,
          title: c.topic || names || 'Chat',
          kind: (c.chatType && chatKinds[c.chatType]) || null,
          preview: cleanPreview(preview && sender ? `${sender}: ${preview}` : preview),
          updated: last?.createdDateTime ?? c.lastUpdatedDateTime ?? null,
          link: c.webUrl ?? null,
        };
      });
    }),
  })),
);

export const NotebooksStore = signalStore(
  withMicrosoftResource<OneNoteNotebook>('notebooks'),
  withComputed((store) => ({
    notebooks: computed(() =>
      store
        .items()
        .map((n) => ({
          id: n.id,
          name: n.displayName || 'Untitled notebook',
          shared: n.isShared === true,
          modified: n.lastModifiedDateTime ?? null,
          link: n.links?.oneNoteWebUrl?.href ?? null,
        }))
        .sort((a, b) => (b.modified ?? '').localeCompare(a.modified ?? '')),
    ),
  })),
);

export type DriveKind = 'folder' | 'document' | 'spreadsheet' | 'presentation' | 'pdf' | 'image' | 'archive' | 'file';

const extensionKinds: Record<string, DriveKind> = {
  doc: 'document', docx: 'document', odt: 'document', rtf: 'document', txt: 'document', md: 'document',
  xls: 'spreadsheet', xlsx: 'spreadsheet', ods: 'spreadsheet', csv: 'spreadsheet',
  ppt: 'presentation', pptx: 'presentation', odp: 'presentation', key: 'presentation',
  pdf: 'pdf',
  png: 'image', jpg: 'image', jpeg: 'image', gif: 'image', webp: 'image', svg: 'image', bmp: 'image', heic: 'image',
  zip: 'archive', rar: 'archive', '7z': 'archive', tar: 'archive', gz: 'archive',
};

const extensionOf = (name: string): string => (name.includes('.') ? (name.split('.').pop() ?? '').toLowerCase() : '');

const kindOf = (item: DriveItem, name: string): DriveKind => {
  if (item.folder) return 'folder';
  const mime = item.file?.mimeType ?? '';
  if (mime.startsWith('image/')) return 'image';
  if (mime === 'application/pdf') return 'pdf';
  if (/zip|compressed|tar|rar/.test(mime)) return 'archive';
  if (/spreadsheet|ms-excel|csv/.test(mime)) return 'spreadsheet';
  if (/presentation|powerpoint/.test(mime)) return 'presentation';
  if (/wordprocessing|msword|opendocument\.text|rtf|^text\//.test(mime)) return 'document';
  return extensionKinds[extensionOf(name)] ?? 'file';
};

export const DriveStore = signalStore(
  withMicrosoftResource<DriveItem>('files', { path: 'root' }, { pageSize: 100, max: 1000 }),
  withComputed((store) => ({
    entries: computed(() =>
      store
        .items()
        .map((i) => {
          const name = i.name || 'Untitled';
          const kind = kindOf(i, name);
          return {
            id: i.id,
            name,
            kind,
            isFolder: kind === 'folder',
            size: i.size ?? 0,
            childCount: i.folder?.childCount ?? null,
            extension: extensionOf(name),
            modified: i.lastModifiedDateTime ?? null,
            link: i.webUrl ?? null,
            download: i.folder ? null : (i['@microsoft.graph.downloadUrl'] ?? null),
          };
        })
        .sort((a, b) => Number(b.isFolder) - Number(a.isFolder) || a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' })),
    ),
  })),
  withMethods((store) => ({
    /** Opens a folder given as the names from the drive root; an empty trail is the root. */
    openFolder: (trail: string[]) =>
      store.setParam('path', trail.length ? `root:/${trail.map(encodeURIComponent).join('/')}:` : 'root'),
  })),
);
