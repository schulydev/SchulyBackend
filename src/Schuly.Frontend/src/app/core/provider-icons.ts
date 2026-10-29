import {
  lucideBackpack,
  lucideBookOpen,
  lucideBriefcase,
  lucideBuilding2,
  lucideCalendar,
  lucideCalendarDays,
  lucideChartColumn,
  lucideCloud,
  lucideFileText,
  lucideFolder,
  lucideGlobe,
  lucideGraduationCap,
  lucideHardDrive,
  lucideKeyRound,
  lucideLibrary,
  lucideLink,
  lucideMail,
  lucideMessageSquare,
  lucideMessagesSquare,
  lucideNotebook,
  lucideNotebookPen,
  lucideSchool,
  lucideShieldCheck,
  lucideUserX,
  lucideUsers,
} from '@ng-icons/lucide';

/**
 * Lucide icons a provider or capability may name (kebab-case, as the contract sends them).
 * A name outside this set falls back to a link icon, so the bundle does not carry the whole Lucide set.
 */
const registry: Record<string, string> = {
  backpack: lucideBackpack,
  'book-open': lucideBookOpen,
  briefcase: lucideBriefcase,
  'building-2': lucideBuilding2,
  calendar: lucideCalendar,
  'calendar-days': lucideCalendarDays,
  'chart-column': lucideChartColumn,
  cloud: lucideCloud,
  'file-text': lucideFileText,
  folder: lucideFolder,
  globe: lucideGlobe,
  'graduation-cap': lucideGraduationCap,
  'hard-drive': lucideHardDrive,
  'key-round': lucideKeyRound,
  library: lucideLibrary,
  link: lucideLink,
  mail: lucideMail,
  'message-square': lucideMessageSquare,
  'messages-square': lucideMessagesSquare,
  notebook: lucideNotebook,
  'notebook-pen': lucideNotebookPen,
  school: lucideSchool,
  'shield-check': lucideShieldCheck,
  'user-x': lucideUserX,
  users: lucideUsers,
};

export const PROVIDER_ICON_FALLBACK = 'lucideLink';

const camel = (kebab: string): string => 'lucide' + kebab.split('-').map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join('');

/** Icon set for provideIcons(): keys are the usual `lucideXxx` names. */
export const providerIcons: Record<string, string> = Object.fromEntries(Object.entries(registry).map(([name, svg]) => [camel(name), svg]));

/** The registered ng-icon key for a Lucide name (kebab-case), or the fallback. */
export function providerIconName(lucide: string | null | undefined): string {
  const key = lucide ? camel(lucide) : '';
  return key in providerIcons ? key : PROVIDER_ICON_FALLBACK;
}
