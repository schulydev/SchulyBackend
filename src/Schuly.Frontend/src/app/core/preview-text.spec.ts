import { describe, expect, it } from 'vitest';
import { cleanPreview } from './preview-text';
import { safeFileName } from '../stores/mail-client';
import { parseAddresses } from '../shared/recipient-input';

describe('cleanPreview', () => {
  it('collapses runs of separator characters', () => {
    expect(cleanPreview(`Hello ${'_'.repeat(300)} bye`)).toBe('Hello bye');
    expect(cleanPreview('a ===== b ----- c ~~~~~ d *****')).toBe('a b c d');
  });

  it('keeps ordinary punctuation', () => {
    expect(cleanPreview('well-known a_b -- ok')).toBe('well-known a_b -- ok');
  });
});

describe('safeFileName', () => {
  it('removes path parts and leading dots', () => {
    expect(safeFileName('../../etc/passwd')).toBe('_etc_passwd');
    expect(safeFileName('..\evil.exe')).toBe('_evil.exe');
    expect(safeFileName('')).toBe('attachment');
  });
});

describe('parseAddresses', () => {
  it('splits, unwraps names and reports invalid entries', () => {
    expect(parseAddresses('a@b.ch, Max <m@x.org>; nope')).toEqual({ valid: ['a@b.ch', 'm@x.org'], invalid: ['nope'] });
  });
});
