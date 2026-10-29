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
  /** Requested page sizes; they grow with "load more" and the background refreshes keep them. */
  chatsTop: number;
  chatsMore: boolean;
  chatsLoadingMore: boolean;
  chatsMoreFailed: boolean;
  chatsLoadedMore: { count: number } | null;
  messagesTop: number;
  messagesMore: boolean;
  messagesLoadingMore: boolean;
  messagesMoreFailed: boolean;
  messagesLoadedMore: { count: number } | null;
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
const CHATS_PAGE = 25;
const CHATS_MAX = 200;
const MESSAGES_PAGE = 30;
const MESSAGES_MAX = 300;

const firstPages = {
  chatsTop: CHATS_PAGE,
  chatsMore: false,
  chatsLoadingMore: false,
  chatsMoreFailed: false,
  chatsLoadedMore: null,
};
const firstMessagePages = {
  messagesTop: MESSAGES_PAGE,
  messagesMore: false,
  messagesLoadingMore: false,
  messagesMoreFailed: false,
  messagesLoadedMore: null,
};

type LoadMode = 'initial' | 'silent' | 'more';

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
    ...firstPages,
    ...firstMessagePages,
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
    // A response is dropped when a newer request for the same list started after it.
    let chatsSeq = 0;
    let messagesSeq = 0;

    async function fetchChats(mode: LoadMode): Promise<void> {
      const accountId = accounts.selectedId();
      if (!accountId) return;
      const seq = ++chatsSeq;
      const top = store.chatsTop();
      if (mode === 'initial') patchState(store, { chatsLoading: true, problem: null });
      try {
        const items = await firstValueFrom(http.get<ChatSummary[]>(`${base(accountId)}/chats`, { params: { top } }));
        if (accounts.selectedId() !== accountId || seq !== chatsSeq) return;
        const chats = Array.isArray(items) ? items : [];
        patchState(store, {
          chats,
          chatsLoading: false,
          problem: null,
          chatsMore: chats.length >= top && top < CHATS_MAX,
          chatsLoadingMore: false,
          chatsMoreFailed: false,
          chatsLoadedMore: mode === 'more' ? { count: Math.max(0, chats.length - store.chats().length) } : store.chatsLoadedMore(),
        });
      } catch (error) {
        if (accounts.selectedId() !== accountId || seq !== chatsSeq) return;
        if (mode === 'more' || store.chatsLoadingMore()) {
          // Keep the list and let the user retry; the page size goes back to what is shown.
          patchState(store, {
            chatsLoadingMore: false,
            chatsMoreFailed: true,
            chatsTop: Math.max(CHATS_PAGE, store.chats().length),
          });
        } else if (mode === 'initial' || !store.chats().length) {
          // A failed background refresh keeps what is on screen.
          patchState(store, { chatsLoading: false, problem: problemOf(error) });
        }
      }
    }

    const loadChats = (silent = false) => fetchChats(silent ? 'silent' : 'initial');

    async function fetchMessages(mode: LoadMode): Promise<void> {
      const accountId = accounts.selectedId();
      const chatId = store.openId();
      if (!accountId || !chatId) return;
      const seq = ++messagesSeq;
      const top = store.messagesTop();
      if (mode === 'initial') patchState(store, { messagesLoading: true, messagesProblem: null });
      try {
        const items = await firstValueFrom(
          http.get<ChatMessage[]>(`${chatUrl(accountId, chatId)}/messages`, { params: { top } }),
        );
        if (accounts.selectedId() !== accountId || store.openId() !== chatId || seq !== messagesSeq) return;
        const messages = (Array.isArray(items) ? items : []).slice().reverse();
        patchState(store, {
          messages,
          messagesLoading: false,
          messagesProblem: null,
          messagesMore: messages.length >= top && top < MESSAGES_MAX,
          messagesLoadingMore: false,
          messagesMoreFailed: false,
          messagesLoadedMore:
            mode === 'more' ? { count: Math.max(0, messages.length - store.messages().length) } : store.messagesLoadedMore(),
        });
      } catch (error) {
        if (accounts.selectedId() !== accountId || store.openId() !== chatId || seq !== messagesSeq) return;
        if (mode === 'more' || store.messagesLoadingMore()) {
          patchState(store, {
            messagesLoadingMore: false,
            messagesMoreFailed: true,
            messagesTop: Math.max(MESSAGES_PAGE, store.messages().length),
          });
        } else if (mode === 'initial' || !store.messages().length) {
          patchState(store, { messagesLoading: false, messagesProblem: problemOf(error) });
        }
      }
    }

    const loadMessages = (silent: boolean) => fetchMessages(silent ? 'silent' : 'initial');

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
      /** Asks for one more page of chats; the list on screen stays until the longer one arrives. */
      loadMoreChats(): void {
        if (!store.chatsMore() || store.chatsLoadingMore() || store.chatsLoading()) return;
        patchState(store, {
          chatsTop: Math.min(store.chatsTop() + CHATS_PAGE, CHATS_MAX),
          chatsLoadingMore: true,
          chatsMoreFailed: false,
        });
        void fetchChats('more');
      },
      /** Asks for older messages of the open chat. */
      loadOlderMessages(): void {
        if (!store.messagesMore() || store.messagesLoadingMore() || store.messagesLoading()) return;
        patchState(store, {
          messagesTop: Math.min(store.messagesTop() + MESSAGES_PAGE, MESSAGES_MAX),
          messagesLoadingMore: true,
          messagesMoreFailed: false,
        });
        void fetchMessages('more');
      },
      async open(chatId: string): Promise<void> {
        messagesSeq++;
        patchState(store, { openId: chatId, messages: [], messagesProblem: null, ...firstMessagePages });
        await loadMessages(false);
        if (store.openId() === chatId && !store.messagesProblem()) await markRead();
      },
      close: () => {
        messagesSeq++;
        patchState(store, { openId: null, messages: [], messagesProblem: null, messagesLoading: false, ...firstMessagePages });
      },
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
          ...firstPages,
          ...firstMessagePages,
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
