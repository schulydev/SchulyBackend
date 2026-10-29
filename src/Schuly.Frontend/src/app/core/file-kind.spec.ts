import { describe, expect, it } from 'vitest';
import { fileKindOf } from './file-kind';

describe('fileKindOf', () => {
  it('trusts a known server kind', () => {
    expect(fileKindOf('x.bin', null, 'excel')).toBe('excel');
  });
  it('derives from content type and extension', () => {
    expect(fileKindOf('Report.DOCX')).toBe('word');
    expect(fileKindOf('a', 'image/png')).toBe('image');
    expect(fileKindOf('clip.webm', 'video/webm')).toBe('video');
    expect(fileKindOf('noext', 'application/pdf')).toBe('pdf');
    expect(fileKindOf('weird.xyz')).toBe('other');
  });
});
