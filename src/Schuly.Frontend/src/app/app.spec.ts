import { TestBed } from '@angular/core/testing';
import { weightedAverage } from './stores/grades.store';

describe('weightedAverage', () => {
  beforeEach(() => TestBed.configureTestingModule({}));

  it('weights each grade', () => {
    expect(weightedAverage([{ score: 6, weighting: 1 }, { score: 4, weighting: 3 }])).toBe(4.5);
  });

  it('returns 0 without grades', () => {
    expect(weightedAverage([])).toBe(0);
  });
});
