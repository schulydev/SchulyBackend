import { describe, expect, it } from 'vitest';
import { compose } from './rich-html';

const root = (html: string) => {
  const el = document.createElement('div');
  el.innerHTML = html;
  return el;
};

describe('compose', () => {
  it('keeps only the supported tags', () => {
    const out = compose(root('<b>bold</b> <i>it</i> <u>un</u> <strike>st</strike> <span style="color:red">plain</span> <script>x</script>'));
    expect(out.html).toBe('<p><strong>bold</strong> <em>it</em> <u>un</u> <s>st</s> plain x</p>');
    expect(out.plain).toBe(false);
  });

  it('turns lists, quotes and code blocks into clean blocks', () => {
    const out = compose(root('<ul><li>a</li><li>b</li></ul><ol><li>c</li></ol><blockquote>q</blockquote><pre>l1\nl2</pre>'));
    expect(out.html).toBe('<ul><li>a</li><li>b</li></ul><ol><li>c</li></ol><blockquote><p>q</p></blockquote><pre><code>l1\nl2</code></pre>');
  });

  it('drops unsafe links and escapes text', () => {
    const out = compose(root('<a href="javascript:alert(1)">bad</a> <a href="https://a.test/?x=1&y=2">ok</a> 1 &lt; 2'));
    expect(out.html).toBe('<p>bad <a href="https://a.test/?x=1&amp;y=2">ok</a> 1 &lt; 2</p>');
  });

  it('numbers inline images from 1 and records their ids', () => {
    const out = compose(root('hi <img data-inline-id="a" alt="x"> and <img data-inline-id="b">'));
    expect(out.html).toContain('<img src="../hostedContents/1/$value" alt="x">');
    expect(out.html).toContain('<img src="../hostedContents/2/$value" alt="">');
    expect(out.imageIds).toEqual(['a', 'b']);
    expect(out.empty).toBe(false);
  });

  it('writes mentions as at tags with an index', () => {
    const out = compose(root('hey <span data-mention-id="u1" class="mention">Ann Lee</span>&nbsp;and <span data-mention-id="u2">Bo</span>'));
    expect(out.html).toBe('<p>hey <at id="0">Ann Lee</at> and <at id="1">Bo</at></p>');
    expect(out.mentions).toEqual([
      { id: 0, text: 'Ann Lee', userId: 'u1' },
      { id: 1, text: 'Bo', userId: 'u2' },
    ]);
  });

  it('treats line breaks and paragraphs as plain text', () => {
    const out = compose(root('one<br>two<div>three</div>'));
    expect(out.plain).toBe(true);
    expect(out.text).toBe('one\ntwo\nthree');
  });

  it('is empty for whitespace and stray breaks', () => {
    expect(compose(root('<br>')).empty).toBe(true);
    expect(compose(root('&nbsp; <br>')).empty).toBe(true);
  });
});

describe('compose emoji', () => {
  it('writes catalogue emojis as emoji tags and keeps their character in the text', () => {
    const out = compose(root('hi <img data-emoji-id="100" alt="&#128175;" title="hundred points"> there'));
    expect(out.html).toBe('<p>hi <emoji id="100" alt="\u{1F4AF}" title="hundred points"></emoji> there</p>');
    expect(out.text).toBe('hi \u{1F4AF} there');
    expect(out.plain).toBe(false);
    expect(out.empty).toBe(false);
  });

  it('keeps a message that is only an emoji', () => {
    const out = compose(root('<img data-emoji-id="100" alt="x" title="hundred points">'));
    expect(out.empty).toBe(false);
    expect(out.html).toBe('<p><emoji id="100" alt="x" title="hundred points"></emoji></p>');
  });
});
