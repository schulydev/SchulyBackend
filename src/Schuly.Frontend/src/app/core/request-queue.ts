/** Runs async jobs with at most `limit` in flight; the rest wait in order. */
export function createQueue(limit: number): <T>(job: () => Promise<T>) => Promise<T> {
  let active = 0;
  const waiting: (() => void)[] = [];
  const pump = () => {
    while (active < limit && waiting.length) {
      active++;
      waiting.shift()!();
    }
  };
  return <T>(job: () => Promise<T>) =>
    new Promise<T>((resolve, reject) => {
      waiting.push(() => {
        job()
          .then(resolve, reject)
          .finally(() => {
            active--;
            pump();
          });
      });
      pump();
    });
}
