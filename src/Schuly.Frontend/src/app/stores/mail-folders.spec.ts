import { describe, expect, it } from 'vitest';
import { orderFolders } from './mail-client';

const f = (id: string, displayName: string, extra: Record<string, unknown> = {}) => ({ id, displayName, ...extra });

const GERMAN = [
  f('a', 'Archiv'),
  f('d', 'Entwürfe'),
  f('x', 'Gelöschte Elemente'),
  f('s', 'Gesendete Elemente'),
  f('j', 'Junk-E-Mail'),
  f('o', 'Postausgang'),
  f('i', 'Posteingang'),
  f('u2', 'Zebra'),
  f('u1', 'Alpha'),
  f('c', 'Konversationsverlauf'),
  f('h', 'Unterhaltungsverlauf'),
  f('sync', 'Synchronisierungsprobleme', { childFolders: [f('k', 'Konflikte'), f('l', 'Lokale Fehler'), f('v', 'Serverfehler')] }),
  f('m', 'Umzugs-Auftrag'),
];

describe('orderFolders', () => {
  it('orders a German mailbox like Outlook, with system folders under More', () => {
    const folders = orderFolders(GERMAN);
    const main = folders.filter((x) => x.group === 'main').map((x) => x.name);
    const more = folders.filter((x) => x.group === 'more').map((x) => `${x.depth}:${x.name}`);
    expect(main).toEqual(['Posteingang', 'Entwürfe', 'Gesendete Elemente', 'Gelöschte Elemente', 'Junk-E-Mail', 'Archiv', 'Alpha', 'Konversationsverlauf', 'Zebra']);
    expect(more).toEqual(['0:Postausgang', '0:Unterhaltungsverlauf', '0:Synchronisierungsprobleme', '1:Konflikte', '1:Lokale Fehler', '1:Serverfehler', '0:Umzugs-Auftrag']);
  });

  it('prefers wellKnownName over the display name', () => {
    const folders = orderFolders([f('1', 'Whatever', { wellKnownName: 'inbox' }), f('2', 'Alpha'), f('3', 'Gesendet', { wellKnownName: 'sentitems' })]);
    expect(folders.map((x) => x.name)).toEqual(['Whatever', 'Gesendet', 'Alpha']);
    expect(folders[0].key).toBe('inbox');
  });
});
