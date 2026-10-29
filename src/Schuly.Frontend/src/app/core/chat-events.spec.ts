import { describe, expect, it } from 'vitest';
import { describeEvent, durationText } from './chat-events';

const names: Record<string, string> = { u1: 'Alice', u2: 'Bob', me: 'Me' };
const nameOf = (id: string) => names[id] ?? null;
const init = { user: { id: 'u1' } };

describe('describeEvent', () => {
  it('describes members being added and removed', () => {
    expect(describeEvent({ '@odata.type': '#microsoft.graph.membersAddedEventMessageDetail', initiator: init, members: [{ id: 'u2' }] }, nameOf, 'me')).toBe('Alice added Bob');
    expect(describeEvent({ '@odata.type': '#microsoft.graph.membersDeletedEventMessageDetail', initiator: init, members: [{ id: 'u2' }, { id: 'me' }] }, nameOf, 'me')).toBe('Alice removed Bob and You');
  });
  it('says You for own actions', () => {
    expect(describeEvent({ '@odata.type': '#microsoft.graph.membersAddedEventMessageDetail', initiator: { user: { id: 'me' } }, members: [{ id: 'u2' }] }, nameOf, 'me')).toBe('You added Bob');
  });
  it('describes renames', () => {
    expect(describeEvent({ '@odata.type': '#microsoft.graph.chatRenamedEventMessageDetail', initiator: init, chatDisplayName: 'Project' }, nameOf, 'me')).toBe('Alice changed the chat name to Project');
  });
  it('describes calls', () => {
    expect(describeEvent({ '@odata.type': '#microsoft.graph.callEndedEventMessageDetail', callDuration: 'PT5M12S' }, nameOf, 'me')).toBe('Call ended, 5 min');
  });
  it('is null for unknown or empty events', () => {
    expect(describeEvent({ '@odata.type': '#microsoft.graph.somethingNew' }, nameOf, 'me')).toBeNull();
    expect(describeEvent(null, nameOf, 'me')).toBeNull();
    expect(describeEvent({ '@odata.type': '#microsoft.graph.membersAddedEventMessageDetail', members: [] }, nameOf, 'me')).toBeNull();
  });
});

describe('durationText', () => {
  it('formats', () => {
    expect(durationText('PT1H2M')).toBe('1 h 2 min');
    expect(durationText('PT45S')).toBe('45 s');
    expect(durationText('x')).toBe('');
  });
});
