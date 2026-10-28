import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { ExamDto, ExamsService } from '../api';
import { startOfToday } from '../core/dates';
import { ClassesStore } from './classes.store';
import { withCollection } from './with-collection';

export interface GradeRow {
  id: string;
  subject: string;
  exam: string;
  date: string | null;
  score: number;
  weighting: number;
  classAverage: number;
}

export interface SubjectAverage {
  subject: string;
  average: number;
  count: number;
}

export interface ExamRow {
  id: string;
  subject: string;
  name: string;
  description: string | null;
  date: string | null;
  finalExam: boolean;
  score: number | null;
}

export const GradesStore = signalStore(
  { providedIn: 'root' },
  withCollection<ExamDto>(() => {
    const api = inject(ExamsService);
    return () => api.apiExamsGet();
  }),
  withState<{ query: string; subject: string }>({ query: '', subject: 'all' }),
  withComputed((store, classes = inject(ClassesStore)) => {
    const subjectOf = (exam: ExamDto) => classes.names().get(exam.classId ?? '') ?? 'Other';
    const exams = computed<ExamRow[]>(() =>
      store
        .items()
        .map((exam) => ({
          id: exam.id ?? exam.name,
          subject: subjectOf(exam),
          name: exam.name,
          description: exam.description ?? null,
          date: exam.date ?? null,
          finalExam: exam.type === 'FinalExam',
          score: exam.grades?.find((g) => g.score != null)?.score ?? null,
        }))
        .sort((a, b) => (a.date ?? '').localeCompare(b.date ?? '')),
    );
    const grades = computed<GradeRow[]>(() =>
      store
        .items()
        .flatMap((exam) =>
          (exam.grades ?? [])
            .filter((grade) => grade.score != null)
            .map((grade) => ({
              id: grade.id ?? `${exam.id}`,
              subject: subjectOf(exam),
              exam: exam.name,
              date: exam.date ?? null,
              score: grade.score ?? 0,
              weighting: grade.weighting ?? 1,
              classAverage: exam.classAverage,
            })),
        )
        .sort((a, b) => (b.date ?? '').localeCompare(a.date ?? '')),
    );
    const subjectAverages = computed<SubjectAverage[]>(() => {
      const bySubject = new Map<string, GradeRow[]>();
      for (const grade of grades()) bySubject.set(grade.subject, [...(bySubject.get(grade.subject) ?? []), grade]);
      return [...bySubject.entries()]
        .map(([subject, rows]) => ({ subject, average: weightedAverage(rows), count: rows.length }))
        .sort((a, b) => a.subject.localeCompare(b.subject));
    });
    return {
      exams,
      upcomingExams: computed(() => exams().filter((exam) => exam.date && new Date(exam.date) >= startOfToday())),
      pastExams: computed(() => exams().filter((exam) => !exam.date || new Date(exam.date) < startOfToday()).reverse()),
      grades,
      subjectAverages,
      overallAverage: computed(() => {
        const subjects = subjectAverages();
        return subjects.length ? subjects.reduce((sum, s) => sum + s.average, 0) / subjects.length : null;
      }),
      insufficientCount: computed(() => subjectAverages().filter((s) => s.average < 4).length),
      filteredGrades: computed(() => {
        const query = store.query().trim().toLowerCase();
        return grades().filter(
          (grade) =>
            (store.subject() === 'all' || grade.subject === store.subject()) &&
            (!query || grade.exam.toLowerCase().includes(query) || grade.subject.toLowerCase().includes(query)),
        );
      }),
    };
  }),
  withMethods((store, classes = inject(ClassesStore)) => ({
    setQuery(query: string): void {
      patchState(store, { query });
    },
    setSubject(subject: string): void {
      patchState(store, { subject });
    },
    reload(): void {
      classes.load();
      store.load();
    },
  })),
);

export function weightedAverage(rows: { score: number; weighting: number }[]): number {
  const weight = rows.reduce((sum, r) => sum + r.weighting, 0);
  return weight ? rows.reduce((sum, r) => sum + r.score * r.weighting, 0) / weight : 0;
}
