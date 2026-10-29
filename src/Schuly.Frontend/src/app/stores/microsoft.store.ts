import { computed, inject } from '@angular/core';
import { signalStore, withComputed } from '@ngrx/signals';
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

export const MailStore = signalStore(
  withMicrosoftResource<MailMessage>('mail', { folder: 'inbox', top: 25, unread: false }),
  withComputed((store) => ({
    messages: computed(() =>
      store.items().map((m) => ({
        id: m.id,
        sender: m.from?.emailAddress?.name || m.from?.emailAddress?.address || 'Unknown sender',
        subject: m.subject || '(no subject)',
        preview: m.bodyPreview ?? '',
        received: m.receivedDateTime ?? null,
        unread: m.isRead === false,
        link: m.webLink ?? null,
      })),
    ),
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
          preview: preview && sender ? `${sender}: ${preview}` : preview,
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
