import type { Routes } from '@angular/router';
import { unsavedChangesGuard } from '../../core/guards/unsaved-changes.guard';
import { validTaskIdGuard } from '../../core/guards/valid-task-id.guard';

export const TASK_ROUTES: Routes = [
  {
    path: '',
    title: 'Tasks · Demo Task Manager',
    loadComponent: () => import('./pages/task-list-page/task-list-page').then((m) => m.TaskListPage),
  },
  {
    path: 'new',
    title: 'New task · Demo Task Manager',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./pages/task-form-page/task-form-page').then((m) => m.TaskFormPage),
  },
  {
    path: ':id',
    title: 'Task details · Demo Task Manager',
    canActivate: [validTaskIdGuard],
    loadComponent: () =>
      import('./pages/task-detail-page/task-detail-page').then((m) => m.TaskDetailPage),
  },
  {
    path: ':id/edit',
    title: 'Edit task · Demo Task Manager',
    canActivate: [validTaskIdGuard],
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./pages/task-form-page/task-form-page').then((m) => m.TaskFormPage),
  },
];
