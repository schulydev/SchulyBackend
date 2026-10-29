export function initials(name: string | null | undefined): string {
  return (name ?? '')
    .split(/[\s@.]+/)
    .filter((part) => /^[\p{L}\p{N}]/u.test(part))
    .slice(0, 2)
    .map((part) => part[0].toUpperCase())
    .join('');
}
