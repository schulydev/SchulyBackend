/** A mention found in the editor; `id` is the index used in `<at id="n">`. */
export interface ComposedMention {
  id: number;
  text: string;
  userId: string;
}

export interface Composed {
  /** Clean HTML limited to the supported tags. */
  html: string;
  /** Plain text with line breaks, for the length counter and text-only sends. */
  text: string;
  mentions: ComposedMention[];
  /** The `data-inline-id` of each inline image, in the order of `hostedContents/N`. */
  imageIds: string[];
  /** True when the HTML only holds paragraphs and line breaks, so it can be sent as plain text. */
  plain: boolean;
  empty: boolean;
}

const MARKS: Record<string, string> = { B: 'strong', STRONG: 'strong', I: 'em', EM: 'em', U: 'u', S: 's', STRIKE: 's', DEL: 's', CODE: 'code' };
const BLOCKS = new Set(['P', 'DIV', 'UL', 'OL', 'LI', 'PRE', 'BLOCKQUOTE', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6']);

export const escapeHtml = (text: string): string => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** Only web and mail links survive; anything else (javascript:, data:) is dropped. */
export function safeHref(href: string | null | undefined): string | null {
  const value = (href ?? '').trim();
  return /^(https?:\/\/|mailto:)/i.test(value) ? value : null;
}

/** Turns the contents of the contenteditable into clean, restricted HTML. Pure over the DOM it is given. */
export function compose(root: Node): Composed {
  const mentions: ComposedMention[] = [];
  const imageIds: string[] = [];

  const inline = (node: Node, inPre = false): string => {
    if (node.nodeType === Node.TEXT_NODE) {
      const text = (node.textContent ?? '').replace(/ /g, ' ').replace(/​/g, '');
      return escapeHtml(text);
    }
    if (node.nodeType !== Node.ELEMENT_NODE) return '';
    const el = node as HTMLElement;
    const tag = el.tagName;
    if (tag === 'BR') return inPre ? '\n' : '<br>';
    if (tag === 'IMG') {
      const emojiId = el.getAttribute('data-emoji-id');
      if (emojiId) {
        // A Teams emoji picked from the catalogue; the server checks the id against the catalogue.
        return `<emoji id="${escapeHtml(emojiId)}" alt="${escapeHtml(el.getAttribute('alt') ?? '')}" title="${escapeHtml(el.getAttribute('title') ?? '')}"></emoji>`;
      }
      const id = el.getAttribute('data-inline-id');
      if (!id) return '';
      imageIds.push(id);
      return `<img src="../hostedContents/${imageIds.length}/$value" alt="${escapeHtml(el.getAttribute('alt') ?? '')}">`;
    }
    const mentionId = el.getAttribute('data-mention-id');
    if (mentionId) {
      const text = (el.textContent ?? '').trim();
      const id = mentions.length;
      mentions.push({ id, text, userId: mentionId });
      return `<at id="${id}">${escapeHtml(text)}</at>`;
    }
    const children = () => Array.from(el.childNodes, (child) => inline(child, inPre)).join('');
    if (tag === 'A') {
      const href = safeHref(el.getAttribute('href'));
      const inner = children();
      return href && inner ? `<a href="${escapeHtml(href)}">${inner}</a>` : inner;
    }
    const mark = MARKS[tag];
    if (mark && !inPre) {
      const inner = children();
      return inner.trim() ? `<${mark}>${inner}</${mark}>` : inner;
    }
    return children();
  };

  const paragraph = (nodes: Node[]): string => {
    let html = nodes.map((n) => inline(n)).join('');
    html = html.replace(/(<br>)+$/, '').replace(/^(<br>)+/, '');
    return html.replace(/<[^>]+>/g, '').trim() || /<(img|at|emoji)\b/.test(html) ? `<p>${html}</p>` : '';
  };

  const blocks = (nodes: Node[]): string => {
    let out = '';
    let run: Node[] = [];
    const flush = () => {
      if (run.length) out += paragraph(run);
      run = [];
    };
    for (const node of nodes) {
      const tag = node.nodeType === Node.ELEMENT_NODE ? (node as HTMLElement).tagName : '';
      if (!BLOCKS.has(tag)) {
        run.push(node);
        continue;
      }
      flush();
      const el = node as HTMLElement;
      const kids = Array.from(el.childNodes);
      if (tag === 'UL' || tag === 'OL') {
        const items = kids.filter((k) => (k as HTMLElement).tagName === 'LI').map((li) => listItem(li as HTMLElement));
        const body = items.join('');
        if (body) out += `<${tag.toLowerCase()}>${body}</${tag.toLowerCase()}>`;
      } else if (tag === 'LI') {
        out += paragraph(kids);
      } else if (tag === 'PRE') {
        const code = kids.map((k) => inline(k, true)).join('').replace(/\n$/, '');
        if (code.trim()) out += `<pre><code>${code}</code></pre>`;
      } else if (tag === 'BLOCKQUOTE') {
        const inner = blocks(kids);
        if (inner) out += `<blockquote>${inner}</blockquote>`;
      } else if (kids.some((k) => k.nodeType === Node.ELEMENT_NODE && BLOCKS.has((k as HTMLElement).tagName))) {
        out += blocks(kids);
      } else {
        out += paragraph(kids);
      }
    }
    flush();
    return out;
  };

  const listItem = (li: HTMLElement): string => {
    const kids = Array.from(li.childNodes);
    const own = kids.filter((k) => !((k as HTMLElement).tagName === 'UL' || (k as HTMLElement).tagName === 'OL'));
    const nested = kids.filter((k) => (k as HTMLElement).tagName === 'UL' || (k as HTMLElement).tagName === 'OL');
    const text = own.map((n) => inline(n)).join('').replace(/(<br>)+$/, '');
    const sub = blocks(nested);
    return text.replace(/<[^>]+>/g, '').trim() || sub ? `<li>${text}${sub}</li>` : '';
  };

  const html = blocks(Array.from(root.childNodes));
  const text = htmlToText(html);
  const plain = !/<(?!\/?(p|br)\b)[a-z]/i.test(html);
  return { html, text, mentions, imageIds, plain, empty: !text.trim() && imageIds.length === 0 };
}

/** Plain text of the composed HTML: paragraphs and list items on their own lines. */
export function htmlToText(html: string): string {
  const withBreaks = html
    .replace(/<emoji\b[^>]*\balt="([^"]*)"[^>]*><\/emoji>/g, '$1')
    .replace(/<br>/g, '\n')
    .replace(/<\/(p|li|pre|blockquote)>/g, '\n')
    .replace(/<[^>]+>/g, '');
  return withBreaks
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&amp;/g, '&')
    .replace(/\n+$/, '');
}
