type Json = Record<string, unknown>;

const obj = (v: unknown): Json | null => (v && typeof v === 'object' && !Array.isArray(v) ? (v as Json) : null);
const str = (v: unknown): string => (typeof v === 'string' ? v.trim() : '');

/** "PT5M12S" as "5 min"; empty when it cannot be read. */
export function durationText(iso: string): string {
  const m = /^P(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?)?$/.exec(iso);
  if (!m) return '';
  const [h, min, sec] = [Number(m[1] ?? 0), Number(m[2] ?? 0), Math.round(Number(m[3] ?? 0))];
  return [h && `${h} h`, min && `${min} min`, sec && !h && !min && `${sec} s`].filter(Boolean).join(' ');
}

const list = (names: string[]): string => (names.length <= 1 ? (names[0] ?? '') : `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`);

/**
 * A short sentence for a system message ("Alice added Bob", "Chat name changed to X"), or null when the event says nothing
 * useful (an unknown type, or one without details), so it can be left out of the conversation.
 * `nameOf` resolves a user id to a name; `meId` makes the user's own actions read "You".
 */
export function describeEvent(detail: Json | null | undefined, nameOf: (id: string) => string | null, meId: string | null): string | null {
  const d = obj(detail);
  if (!d) return null;
  const type = str(d['@odata.type']).replace(/^#?microsoft\.graph\./, '');
  const who = (identity: unknown): string => {
    const user = obj(obj(identity)?.['user']) ?? obj(identity);
    const id = str(user?.['id']);
    if (id && meId && id === meId) return 'You';
    return str(user?.['displayName']) || (id ? nameOf(id) : null) || '';
  };
  const initiator = who(d['initiator']);
  const rawMembers = Array.isArray(d['members']) ? (d['members'] as unknown[]) : [];
  const members = rawMembers.map(who).filter(Boolean);
  const target = members.length ? list(members) : rawMembers.length ? (rawMembers.length === 1 ? 'a member' : `${rawMembers.length} members`) : '';

  switch (type) {
    case 'membersAddedEventMessageDetail':
      if (!target) return null;
      return initiator ? `${initiator} added ${target}` : `${target} ${members.length > 1 ? 'were' : 'was'} added`;
    case 'membersDeletedEventMessageDetail':
      if (!target) return null;
      return initiator ? `${initiator} removed ${target}` : `${target} ${members.length > 1 ? 'were' : 'was'} removed`;
    case 'membersJoinedEventMessageDetail':
      return target ? `${target} joined` : null;
    case 'membersLeftEventMessageDetail':
      return target ? `${target} left` : null;
    case 'chatRenamedEventMessageDetail': {
      const name = str(d['chatDisplayName']);
      if (!name) return initiator ? `${initiator} removed the chat name` : 'Chat name removed';
      return initiator ? `${initiator} changed the chat name to ${name}` : `Chat name changed to ${name}`;
    }
    case 'teamsAppInstalledEventMessageDetail':
    case 'teamsAppRemovedEventMessageDetail': {
      const app = str(d['teamsAppDisplayName']);
      if (!app) return null;
      return `${app} was ${type.includes('Installed') ? 'added to' : 'removed from'} the chat`;
    }
    case 'callEndedEventMessageDetail': {
      const time = durationText(str(d['callDuration']));
      return time ? `Call ended, ${time}` : 'Call ended';
    }
    case 'callStartedEventMessageDetail':
      return initiator ? `${initiator} started a call` : 'Call started';
    default:
      return null;
  }
}
