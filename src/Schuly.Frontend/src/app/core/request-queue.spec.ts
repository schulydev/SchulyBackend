import { describe, expect, it } from 'vitest';
import { createQueue } from './request-queue';

describe('createQueue', () => {
  it('never runs more than the limit at once', async () => {
    const run = createQueue(4);
    let active = 0;
    let peak = 0;
    const job = async () => {
      peak = Math.max(peak, ++active);
      await new Promise((r) => setTimeout(r, 5));
      active--;
    };
    await Promise.all(Array.from({ length: 12 }, () => run(job)));
    expect(peak).toBe(4);
  });
});
