import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth';

export const routes: Routes = [
  { path: 'login', title: 'Sign in', canActivate: [guestGuard], loadComponent: () => import('./pages/login').then((m) => m.Login) },
  { path: 'callback', loadComponent: () => import('./pages/callback').then((m) => m.Callback) },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/app-layout').then((m) => m.AppLayout),
    children: [
      { path: '', title: 'Overview', loadComponent: () => import('./pages/overview').then((m) => m.Overview) },
      { path: 'grades', title: 'Grades', loadComponent: () => import('./pages/grades').then((m) => m.Grades) },
      { path: 'exams', title: 'Exams', loadComponent: () => import('./pages/exams').then((m) => m.Exams) },
      { path: 'agenda', title: 'Agenda', loadComponent: () => import('./pages/agenda').then((m) => m.Agenda) },
      { path: 'absences', title: 'Absences', loadComponent: () => import('./pages/absences').then((m) => m.Absences) },
      { path: 'documents', title: 'Documents', loadComponent: () => import('./pages/documents').then((m) => m.Documents) },
      { path: 'settings', title: 'Settings', loadComponent: () => import('./pages/settings').then((m) => m.Settings) },
    ],
  },
  { path: '**', redirectTo: '' },
];
