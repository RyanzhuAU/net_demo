import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { AppConfigService } from '../../../../core/config/app-config.service';
import { apiBaseUrlInterceptor } from '../../../../core/interceptors/api-base-url.interceptor';
import { errorInterceptor } from '../../../../core/interceptors/error.interceptor';
import { TaskFormPage } from './task-form-page';

describe('TaskFormPage', () => {
  let fixture: ComponentFixture<TaskFormPage>;
  let component: TaskFormPage;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TaskFormPage],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiBaseUrlInterceptor, errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    TestBed.inject(AppConfigService).set({ apiBaseUrl: '/api' });
    httpMock = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(TaskFormPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('starts invalid because the title is required', () => {
    expect(component['form'].valid).toBe(false);
    expect(component['form'].controls.title.hasError('required')).toBe(true);
  });

  it('does not submit while the form is invalid', () => {
    const formElement: HTMLFormElement = fixture.nativeElement.querySelector('form');
    formElement.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    httpMock.expectNone('/api/tasks');
    expect(component['form'].controls.title.touched).toBe(true);
  });

  it('shows a validation message once the title control is touched', () => {
    const input: HTMLInputElement = fixture.nativeElement.querySelector('[data-testid="title-input"]');
    input.value = '';
    input.dispatchEvent(new Event('input'));
    component['form'].controls.title.markAsTouched();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Title is required.');
  });

  it('rejects a title longer than the server-side limit', () => {
    component['form'].controls.title.setValue('x'.repeat(201));
    expect(component['form'].controls.title.hasError('maxlength')).toBe(true);
  });

  it('posts a create request with a normalised payload', () => {
    component['form'].setValue({
      title: '  Ship the demo  ',
      description: '   ',
      status: 'InProgress',
      dueDateLocal: '',
    });

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));

    const request = httpMock.expectOne('/api/tasks');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      title: 'Ship the demo',
      description: null,
      status: 'InProgress',
      dueDateUtc: null,
    });
  });

  it('surfaces server-side field errors returned as ProblemDetails', () => {
    component['form'].setValue({
      title: 'Valid title',
      description: '',
      status: 'Todo',
      dueDateLocal: '',
    });

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));

    httpMock.expectOne('/api/tasks').flush(
      {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { Title: ['Title is already used.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(component.serverErrorsFor('title')).toEqual(['Title is already used.']);
    expect(fixture.nativeElement.textContent).toContain('Title is already used.');
  });

  it('reports unsaved changes only while the form is dirty', () => {
    expect(component.hasUnsavedChanges()).toBe(false);

    component['form'].controls.title.setValue('Draft');
    component['form'].markAsDirty();

    expect(component.hasUnsavedChanges()).toBe(true);
  });
});
