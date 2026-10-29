import { describe, expect, it } from 'vitest';
import { LinkProviderDto, LinkedAccountDto } from '../core/linking-api';
import { capabilityAccountsOf, navGroupsOf } from './linked-accounts.store';

const account = (extra: Partial<LinkedAccountDto>): LinkedAccountDto => ({
  providerId: 'prov-a',
  providerName: 'Provider A',
  id: 'a1',
  title: 'Account',
  status: 'ok',
  capabilities: [],
  canReauth: false,
  ...extra,
});
const provider = (id: string, sortOrder: number): LinkProviderDto => ({ id, displayName: id, icon: {}, capabilities: [], flows: [], sortOrder });

describe('capabilityAccountsOf', () => {
  it('groups accounts by capability kind with the declared base path', () => {
    const result = capabilityAccountsOf([
      account({ capabilities: [{ kind: 'mail', label: 'Mail', basePath: '/api/x/accounts/' }, { kind: 'school_data', label: 'Data' }] }),
      account({ providerId: 'prov-b', providerName: 'Provider B', id: 'b1', capabilities: [{ kind: 'absence_reporting', label: 'Absences', basePath: '/api/y/accounts' }] }),
    ]);
    expect(result.mail.map((a) => a.basePath)).toEqual(['/api/x/accounts']);
    expect(result.absence_reporting.map((a) => a.id)).toEqual(['b1']);
    expect(result.chat).toEqual([]);
  });

  it('ignores kinds without a UI and capabilities without a base path', () => {
    const result = capabilityAccountsOf([account({ capabilities: [{ kind: 'weather', label: 'W', basePath: '/w' }, { kind: 'drive', label: 'Drive', basePath: null }] })]);
    expect(Object.values(result).flat()).toEqual([]);
  });
});

describe('navGroupsOf', () => {
  it('builds one group per provider with linked accounts, labelled by the provider name and the capability label', () => {
    const groups = navGroupsOf(
      [
        account({ capabilities: [{ kind: 'mail', label: 'Mail', basePath: '/m' }, { kind: 'chat', label: 'Chats', basePath: '/m' }, { kind: 'calendar', label: 'Cal', basePath: '/c' }] }),
        account({ id: 'a2', capabilities: [{ kind: 'mail', label: 'Mail', basePath: '/m' }] }),
      ],
      [provider('prov-a', 0)],
    );
    expect(groups).toHaveLength(1);
    expect(groups[0].label).toBe('Provider A');
    expect(groups[0].items.map((i) => [i.title, i.url])).toEqual([['Mail', '/mail'], ['Chats', '/chat']]);
  });

  it('shows no group for a provider without a page-backed capability and orders by sortOrder', () => {
    const groups = navGroupsOf(
      [
        account({ providerId: 'late', providerName: 'Late', capabilities: [{ kind: 'drive', label: 'Drive', basePath: '/d' }] }),
        account({ providerId: 'early', providerName: 'Early', id: 'e', capabilities: [{ kind: 'notes', label: 'Notes', basePath: '/n' }] }),
        account({ providerId: 'none', providerName: 'None', id: 'n', capabilities: [{ kind: 'school_data', label: 'Data' }] }),
      ],
      [provider('early', 0), provider('late', 5)],
    );
    expect(groups.map((g) => g.label)).toEqual(['Early', 'Late']);
  });
});
