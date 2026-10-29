import { describe, expect, it } from 'vitest';
import { safeApiPath } from './teams-api';

const BASE = '/api/plugins/microsoft/accounts/acc1';
const BS = String.fromCharCode(92);

describe('safeApiPath', () => {
  it('builds relative paths under the account base', () => {
    expect(safeApiPath('chats/c%3A1/messages/m1/media/abc%3D%3D%2Fx', BASE)).toBe(`${BASE}/chats/c%3A1/messages/m1/media/abc%3D%3D%2Fx`);
    expect(safeApiPath('chats/c/messages/m/files/f/thumbnail?size=medium', BASE)).toBe(`${BASE}/chats/c/messages/m/files/f/thumbnail?size=medium`);
  });

  it('accepts absolute paths only under this account', () => {
    expect(safeApiPath(`${BASE}/chats/c/photo`, BASE)).toBe(`${BASE}/chats/c/photo`);
    expect(safeApiPath('/api/plugins/microsoft/accounts/other/chats', BASE)).toBeNull();
    expect(safeApiPath('/api/me/linked-accounts', BASE)).toBeNull();
    expect(safeApiPath('/auth/devices', BASE)).toBeNull();
  });

  it('rejects protocol-relative paths', () => {
    for (const bad of ['//evil.test/x', '///evil.test', '//' + BASE.slice(1)]) expect(safeApiPath(bad, BASE), bad).toBeNull();
  });

  it('rejects schemes', () => {
    for (const bad of ['https://evil.test/x', 'javascript:alert(1)', 'data:text/html,x', 'HTTP://evil.test']) expect(safeApiPath(bad, BASE), bad).toBeNull();
  });

  it('rejects backslashes and control characters', () => {
    for (const bad of [BS + BS + 'evil.test' + BS + 'x', 'chats' + BS + '..' + BS + 'x', '/' + BS + 'evil.test', 'chats/x\n', 'chats/x\u0000']) {
      expect(safeApiPath(bad, BASE), JSON.stringify(bad)).toBeNull();
    }
  });

  it('rejects dot segments, plain or percent-encoded', () => {
    for (const bad of [`${BASE}/../../auth/devices`, 'chats/../../x', 'chats/%2e%2e/x', 'chats/%2E/x', 'chats/./x']) expect(safeApiPath(bad, BASE), bad).toBeNull();
  });

  it('rejects empty and malformed paths', () => {
    for (const bad of ['', 'chats/%zz']) expect(safeApiPath(bad, BASE), bad).toBeNull();
  });
});
