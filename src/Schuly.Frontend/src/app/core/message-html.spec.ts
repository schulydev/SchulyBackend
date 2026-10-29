import { describe, expect, it } from 'vitest';
import { renderBody } from './message-html';

describe('renderBody emoji placeholders', () => {
  it('replaces the whole element, text included, with a slot holding the character', () => {
    const r = renderBody('<p>hi <emoji key="e0" alt="\u{1F4AF}">\u{1F4AF}</emoji> there</p>', 'html');
    expect(r.html).toBe('<p>hi <span class="emoji-slot">\u{1F4AF}</span> there</p>');
    expect(r.emojiKeys).toEqual(['e0']);
    expect(r.emojiOnly).toBe(false);
  });
  it('uses the inner text when there is no alt, and marks emoji-only messages', () => {
    const r = renderBody('<p><emoji key="e1">\u{1F44D}</emoji></p>', 'html');
    expect(r.html).toContain('<span class="emoji-slot">\u{1F44D}</span>');
    expect(r.emojiOnly).toBe(true);
  });
  it('turns a keyless placeholder into plain text', () => {
    const r = renderBody('<p>a <emoji alt="\u{1F600}">\u{1F600}</emoji></p>', 'html');
    expect(r.html).toBe('<p>a \u{1F600}</p>');
    expect(r.emojiKeys).toEqual([]);
  });
  it('drops auth-gated images and keeps https ones', () => {
    const r = renderBody('<p><img src="https://graph.microsoft.com/x/hostedContents/1/$value"><img src="https://ok.test/a.png"></p>', 'html');
    expect(r.removedImages).toBe(1);
    expect(r.html).toContain('ok.test');
  });
  it('removes a listed GIF image from the text', () => {
    const r = renderBody('<p>x <img src="https://media.giphy.com/a.gif"></p>', 'html', { gifUrls: ['https://media.giphy.com/a.gif'] });
    expect(r.html).not.toContain('<img');
    expect(r.removedImages).toBe(0);
  });
});
