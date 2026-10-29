export interface RenderedBody {
  /** HTML to bind with [innerHTML]; Angular's sanitizer runs on it again. Empty for text-only or empty messages. */
  html: string;
  text: string;
  /** Images that need Microsoft sign-in and were removed. */
  removedImages: number;
}

const authImage = /graph\.microsoft\.com|hostedcontents/i;

/** Prepares a Teams message body: drops auth-gated images, scripts and styles, bolds mentions and opens links in a new tab. */
export function renderBody(content: string | null | undefined, contentType: string | null | undefined): RenderedBody {
  const raw = content ?? '';
  if (contentType !== 'html') return { html: '', text: raw, removedImages: 0 };

  const root = new DOMParser().parseFromString(raw, 'text/html').body;
  root.querySelectorAll('script, style, attachment').forEach((el) => el.remove());

  let removedImages = 0;
  root.querySelectorAll('img').forEach((img) => {
    const src = img.getAttribute('src') ?? '';
    if (!src || authImage.test(src) || !/^https:/i.test(src)) {
      removedImages++;
      img.remove();
    }
  });
  root.querySelectorAll('at').forEach((at) => {
    const strong = root.ownerDocument.createElement('strong');
    strong.textContent = at.textContent;
    at.replaceWith(strong);
  });
  root.querySelectorAll('a').forEach((a) => {
    a.setAttribute('target', '_blank');
    a.setAttribute('rel', 'noopener noreferrer');
  });

  const text = (root.textContent ?? '').trim();
  const hasMedia = root.querySelector('img') !== null;
  return { html: text || hasMedia ? root.innerHTML : '', text, removedImages };
}
