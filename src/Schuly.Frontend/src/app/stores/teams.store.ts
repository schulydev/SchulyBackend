import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { computed, effect, inject, untracked } from '@angular/core';
import { patchState, signalStore, withComputed, withHooks, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { MicrosoftAccountsStore } from './microsoft-accounts.store';
import { MicrosoftProblem } from './microsoft-resource';

export interface ChatMember {
  displayName?: string | null;
  userId?: string | null;
  email?: string | null;
}

export interface ChatBody {
  content?: string | null;
  contentType?: string | null;
}

export interface ChatSummary {
  id: string;
  topic?: string | null;
  chatType?: string | null;
  webUrl?: string | null;
  lastUpdatedDateTime?: string | null;
  members?: ChatMember[] | null;
  viewpoint?: { lastMessageReadDateTime?: string | null } | null;
  lastMessagePreview?: {
    createdDateTime?: string | null;
    isDeleted?: boolean | null;
    body?: ChatBody | null;
    from?: { user?: { id?: string | null; displayName?: string | null } | null } | null;
  } | null;
}

export interface ChatMessage {
  id: string;
  createdDateTime?: string | null;
  lastModifiedDateTime?: string | null;
  deletedDateTime?: string | null;
  messageType?: string | null;
  from?: { user?: { id?: string | null; displayName?: string | null } | null } | null;
  body?: ChatBody | null;
  attachments?: { id?: string | null; name?: string | null; contentType?: string | null; contentUrl?: string | null }[] | null;
  mentions?: unknown[] | null;
  reactions?: { reactionType?: string | null }[] | null;
}

/** A message of mine that the server has not confirmed yet. */
export interface PendingMessage {
  localId: string;
  chatId: string;
  text: string;
  createdDateTime: string;
  status: 'sending' | 'failed';
}

export interface Me {
  id: string;
  displayName?: string | null;
  mail?: string | null;
}

interface TeamsState {
  chats: ChatSummary[];
  chatsLoading: boolean;
  problem: MicrosoftProblem | null;
  me: Me | null;
  openId: string | null;
  /** Oldest first. */
  messages: ChatMessage[];
  messagesLoading: boolean;
  messagesProblem: MicrosoftProblem | null;
  pending: PendingMessage[];
  /** Object URL per `${accountId}:${userId}`; null means the user has no photo. */
  photos: Record<string, string | null>;
}

// The plugin answers an expired Microsoft sign-in with 409; a 401 is the Schuly session and the auth interceptor handles it.
const problemOf = (error: unknown): MicrosoftProblem =>
  error instanceof HttpErrorResponse && error.status === 409
    ? 'expired'
    : error instanceof HttpErrorResponse && error.status === 404
      ? 'missing'
      : 'failed';

/** Visible text of a message body; script and style contents are not text a reader sees. */
export function bodyText(body?: ChatBody | null): string {
  const content = body?.content ?? '';
  if (!content) return '';
  let text = content;
  if (body?.contentType === 'html') {
    const root = new DOMParser().parseFromString(content, 'text/html').body;
    root.querySelectorAll('script, style').forEach((element) => element.remove());
    text = root.textContent ?? '';
  }
  return text.replace(/\s+/g, ' ').trim();
}

const chatKinds: Record<string, string> = { oneOnOne: 'Chat', group: 'Group', meeting: 'Meeting' };
const MAX_PHOTO_REQUESTS = 4;

export const TeamsStore = signalStore(
  withState<TeamsState>({
    chats: [],
    chatsLoading: false,
    problem: null,
    me: null,
    openId: null,
    messages: [],
    messagesLoading: false,
    messagesProblem: null,
    pending: [],
    photos: {},
  }),
  withComputed((store, accounts = inject(MicrosoftAccountsStore)) => {
    const meId = computed(() => {
      const me = store.me();
      if (me?.id) return me.id;
      const own = accounts.selected()?.email?.toLowerCase();
      if (!own) return null;
      for (const chat of store.chats()) {
        const found = chat.members?.find((m) => m.email?.toLowerCase() === own && m.userId);
        if (found) return found.userId ?? null;
      }
      return null;
    });

    const chatItems = computed(() => {
      const mine = meId();
      return store.chats().map((c) => {
        const members = (c.members ?? []).filter((m) => m.displayName);
        const others = members.filter((m) => !mine || m.userId !== mine);
        const people = others.length ? others : members;
        const last = c.lastMessagePreview;
        const sender = last?.from?.user;
        const text = last?.isDeleted ? 'This message was deleted' : bodyText(last?.body);
        const read = c.viewpoint?.lastMessageReadDateTime;
        const lastAt = last?.createdDateTime ?? null;
        const unread =
          !!lastAt && !!sender?.id && sender.id !== mine && (!read || new Date(lastAt).getTime() > new Date(read).getTime());
        return {
          id: c.id,
          title: c.topic || people.map((m) => m.displayName).join(', ') || 'Chat',
          kind: (c.chatType && chatKinds[c.chatType]) || null,
          oneOnOne: c.chatType === 'oneOnOne',
          people: people.map((m) => ({ id: m.userId ?? null, name: m.displayName ?? '' })),
          preview: text && sender?.displayName && c.chatType !== 'oneOnOne' ? `${sender.displayName}: ${text}` : text,
          updated: lastAt ?? c.lastUpdatedDateTime ?? null,
          unread,
          link: c.webUrl ?? null,
        };
      });
    });

    return {
      meId,
      waiting: computed(() => !accounts.loaded()),
      chatItems,
      openChat: computed(() => chatItems().find((c) => c.id === store.openId()) ?? null),
    };
  }),
  withMethods((store, http = inject(HttpClient), accounts = inject(MicrosoftAccountsStore)) => {
    const base = (id: string) => `/api/plugins/microsoft/accounts/${encodeURIComponent(id)}`;
    const chatUrl = (id: string, chatId: string) => `${base(id)}/chats/${encodeURIComponent(chatId)}`;

    const photoQueue: (() => void)[] = [];
    let photoActive = 0;
    const photoRequested = new Set<string>();
    const pumpPhotos = () => {
      while (photoActive < MAX_PHOTO_REQUESTS && photoQueue.length) {
        photoActive++;
        photoQueue.shift()!();
      }
    };

    let nextLocal = 0;

    async function loadChats(silent = false): Promise<void> {
      const accountId = accounts.selectedId();
      if (!accountId) return;
      if (!silent) patchState(store, { chatsLoading: true, problem: null });
      try {
        const items = await firstValueFrom(http.get<ChatSummary[]>(`${base(accountId)}/chats`, { params: { top: 25 } }));
        if (accounts.selectedId() !== accountId) return;
        patchState(store, { chats: Array.isArray(items) ? items : [], chatsLoading: false, problem: null });
      } catch (error) {
        if (accounts.selectedId() !== accountId) return;
        // A failed background refresh keeps what is on screen.
        if (!silent || !store.chats().length) patchState(store, { chatsLoading: false, problem: problemOf(error) });
      }
    }

    async function loadMessages(silent: boolean): Promise<void> {
      const accountId = accounts.selectedId();
      const chatId = store.openId();
      if (!accountId || !chatId) return;
      if (!silent) patchState(store, { messagesLoading: true, messagesProblem: null });
      try {
        const items = await firstValueFrom(
          http.get<ChatMessage[]>(`${chatUrl(accountId, chatId)}/messages`, { params: { top: 30 } }),
        );
        if (accounts.selectedId() !== accountId || store.openId() !== chatId) return;
        patchState(store, {
          messages: (Array.isArray(items) ? items : []).slice().reverse(),
          messagesLoading: false,
          messagesProblem: null,
        });
      } catch (error) {
        if (accounts.selectedId() !== accountId || store.openId() !== chatId) return;
        if (!silent || !store.messages().length) {
          patchState(store, { messagesLoading: false, messagesProblem: problemOf(error) });
        }
      }
    }

    async function markRead(): Promise<void> {
      const accountId = accounts.selectedId();
      const chatId = store.openId();
      if (!accountId || !chatId) return;
      try {
        await firstValueFrom(http.post(`${chatUrl(accountId, chatId)}/read`, {}));
        const now = new Date().toISOString();
        patchState(store, {
          chats: store
            .chats()
            .map((c) => (c.id === chatId ? { ...c, viewpoint: { ...c.viewpoint, lastMessageReadDateTime: now } } : c)),
        });
      } catch {
        // Marking as read is best effort.
      }
    }

    async function post(pending: PendingMessage): Promise<void> {
      const accountId = accounts.selectedId();
      if (!accountId) return;
      patchState(store, {
        pending: store.pending().map((p) => (p.localId === pending.localId ? { ...p, status: 'sending' as const } : p)),
      });
      try {
        const created = await firstValueFrom(
          http.post<ChatMessage>(`${chatUrl(accountId, pending.chatId)}/messages`, { text: pending.text }),
        );
        const stillOpen = store.openId() === pending.chatId;
        const known = store.messages().some((m) => m.id === created?.id);
        patchState(store, {
          pending: store.pending().filter((p) => p.localId !== pending.localId),
          messages: stillOpen && created?.id && !known ? [...store.messages(), created] : store.messages(),
        });
        void loadChats(true);
      } catch {
        patchState(store, {
          pending: store.pending().map((p) => (p.localId === pending.localId ? { ...p, status: 'failed' as const } : p)),
        });
      }
    }

    return {
      loadChats,
      refreshMessages: () => loadMessages(true),
      async open(chatId: string): Promise<void> {
        patchState(store, { openId: chatId, messages: [], messagesProblem: null });
        await loadMessages(false);
        if (store.openId() === chatId && !store.messagesProblem()) await markRead();
      },
      close: () => patchState(store, { openId: null, messages: [], messagesProblem: null, messagesLoading: false }),
      markRead,
      send(text: string): void {
        const chatId = store.openId();
        const trimmed = text.trim();
        if (!chatId || !trimmed) return;
        const pending: PendingMessage = {
          localId: `local-${Date.now()}-${nextLocal++}`,
          chatId,
          text: trimmed.slice(0, 4000),
          createdDateTime: new Date().toISOString(),
          status: 'sending',
        };
        patchState(store, { pending: [...store.pending(), pending] });
        void post(pending);
      },
      retry(localId: string): void {
        const pending = store.pending().find((p) => p.localId === localId);
        if (pending && pending.status === 'failed') void post(pending);
      },
      discard: (localId: string) => patchState(store, { pending: store.pending().filter((p) => p.localId !== localId) }),

      /** Asks for a profile photo once per (account, user); the result lands in `photos`. */
      requestPhoto(userId: string): void {
        const accountId = accounts.selectedId();
        if (!accountId || !userId) return;
        const key = `${accountId}:${userId}`;
        if (photoRequested.has(key)) return;
        photoRequested.add(key);
        photoQueue.push(() => {
          firstValueFrom(
            http.get(`${base(accountId)}/photos/${encodeURIComponent(userId)}`, {
              params: { size: 64 },
              responseType: 'blob',
            }),
          )
            .then((blob) => {
              const url = blob.size > 0 ? URL.createObjectURL(blob) : null;
              patchState(store, { photos: { ...store.photos(), [key]: url } });
            })
            .catch(() => patchState(store, { photos: { ...store.photos(), [key]: null } }))
            .finally(() => {
              photoActive--;
              pumpPhotos();
            });
        });
        pumpPhotos();
      },
      photoFor: (userId: string | null | undefined): string | null => {
        const accountId = accounts.selectedId();
        return accountId && userId ? (store.photos()[`${accountId}:${userId}`] ?? null) : null;
      },
      revokePhotos(): void {
        for (const url of Object.values(store.photos())) if (url) URL.revokeObjectURL(url);
        patchState(store, { photos: {} });
      },
      async loadMe(): Promise<void> {
        const accountId = accounts.selectedId();
        if (!accountId) return;
        try {
          const me = await firstValueFrom(http.get<Me>(`${base(accountId)}/me`));
          if (accounts.selectedId() === accountId) patchState(store, { me: me?.id ? me : null });
        } catch {
          // Without /me, own messages are recognised through the account email.
        }
      },
      reset: () =>
        patchState(store, {
          chats: [],
          problem: null,
          me: null,
          openId: null,
          messages: [],
          messagesProblem: null,
          pending: [],
        }),
    };
  }),
  withHooks({
    onInit: (store) => {
      const accounts = inject(MicrosoftAccountsStore);
      effect(() => {
        const id = accounts.selectedId();
        untracked(() => {
          store.reset();
          if (id) {
            void store.loadChats();
            void store.loadMe();
          }
        });
      });
    },
    onDestroy: (store) => store.revokePhotos(),
  }),
);
