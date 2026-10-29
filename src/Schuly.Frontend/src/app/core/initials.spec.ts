import { describe, expect, it } from 'vitest';
import { initials } from './initials';

describe('initials', () => {
  it('takes the first letters of the first two words', () => {
    expect(initials('Ann Lee')).toBe('AL');
    expect(initials('elias.spycher@school.ch')).toBe('ES');
  });
  it('skips words that do not start with a letter', () => {
    expect(initials('Notes (you)')).toBe('N');
    expect(initials('')).toBe('');
  });
});
