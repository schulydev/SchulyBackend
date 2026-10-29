/** Collapses runs of separator characters (underscores, dashes, equals, asterisks, tildes) that mail clients use as rules, so previews stay readable and short. */
export function cleanPreview(text: string | null | undefined): string {
  return (text ?? '')
    .replace(/[_\-=*~]{3,}/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}
