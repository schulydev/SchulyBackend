export function tint(color: string | undefined, amount = 16): string {
  return color ? `color-mix(in oklab, ${color} ${amount}%, transparent)` : 'transparent';
}
