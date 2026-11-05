import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'tasks' },
  {
    path: 'tasks',
    loadChildren: () => import('./features/tasks/task.routes').then((m) => m.TASK_ROUTES),
  },
  {
    path: 'not-found',
    title: 'Not found · Demo Task Manager',
    loadComponent: () => import('./features/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
  { path: '**', redirectTo: 'not-found' },
];
