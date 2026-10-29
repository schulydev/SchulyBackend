export interface RenderedBody {
  /** HTML to bind with [innerHTML]; Angular's sanitizer runs on it again. Empty for text-only or empty messages. */
  html: string;
  text: string;
  /** Images that need the sender's service sign-in and were removed. */
  removedImages: number;
  /** Message keys of the `<emoji key>` placeholders, in document order; each one is a `.emoji-slot` span in `html`. */
  emojiKeys: string[];
  /** The message holds nothing but emojis (and whitespace), so they are shown large. */
  emojiOnly: boolean;
}

const authImage = /graph\.microsoft\.com|hostedcontents/i;

/** Prepares a chat message body: drops auth-gated images, scripts and styles, marks mentions and opens links in a new tab. */
export interface RenderOptions {
  /** Public GIF URLs the message lists in `media`; their `<img>` is dropped from the text because the media block shows them. */
  gifUrls?: string[];
  /** Keep `blob:` images, for a message of mine that is still being sent. */
  allowBlobImages?: boolean;
}

export function renderBody(content: string | null | undefined, contentType: string | null | undefined, options: RenderOptions = {}): RenderedBody {
  const raw = content ?? '';
  if (contentType !== 'html') return { html: '', text: raw, removedImages: 0, emojiKeys: [], emojiOnly: false };

  const root = new DOMParser().parseFromString(raw, 'text/html').body;
  root.querySelectorAll('script, style, attachment').forEach((el) => el.remove());

  const emojiKeys: string[] = [];
  root.querySelectorAll('emoji').forEach((el) => {
    const key = el.getAttribute('key');
    // 0.8.1 also carries the character as the element's text; either one is the fallback when the image cannot load.
    const alt = el.getAttribute('alt') || el.textContent || '';
    if (key) {
      // Angular's sanitizer keeps spans and classes, so each placeholder becomes a slot the view fills in order.
      const slot = root.ownerDocument.createElement('span');
      slot.className = 'emoji-slot';
      slot.textContent = alt;
      emojiKeys.push(key);
      el.replaceWith(slot);
    } else {
      el.replaceWith(root.ownerDocument.createTextNode(alt));
    }
  });

  let removedImages = 0;
  root.querySelectorAll('img').forEach((img) => {
    const src = img.getAttribute('src') ?? '';
    if (options.allowBlobImages && /^blob:/i.test(src)) return;
    if (options.gifUrls?.includes(src)) {
      img.remove();
      return;
    }
    if (!src || authImage.test(src) || !/^https:/i.test(src)) {
      removedImages++;
      img.remove();
    }
  });
  root.querySelectorAll('at').forEach((at) => {
    const strong = root.ownerDocument.createElement('span');
    strong.className = 'mention';
    strong.textContent = at.textContent;
    at.replaceWith(strong);
  });
  root.querySelectorAll('a').forEach((a) => {
    a.setAttribute('target', '_blank');
    a.setAttribute('rel', 'noopener noreferrer');
  });

  const text = (root.textContent ?? '').trim();
  const hasMedia = root.querySelector('img') !== null;
  const bare = root.cloneNode(true) as HTMLElement;
  bare.querySelectorAll('.emoji-slot').forEach((slot) => slot.remove());
  const emojiOnly = emojiKeys.length > 0 && !(bare.textContent ?? '').trim() && bare.querySelector('img') === null;
  return { html: text || hasMedia ? root.innerHTML : '', text, removedImages, emojiKeys, emojiOnly };
}
