import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppConfigService } from '../../../core/config/app-config.service';
import { apiBaseUrlInterceptor } from '../../../core/interceptors/api-base-url.interceptor';
import { AboutPage } from './about-page';

describe('AboutPage', () => {
  let fixture: ComponentFixture<AboutPage>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AboutPage],
      providers: [
        provideHttpClient(withInterceptors([apiBaseUrlInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    TestBed.inject(AppConfigService).set({ apiBaseUrl: '/api' });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AboutPage);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads the About DTO from /api/about and renders it', async () => {
    expect(fixture.nativeElement.querySelector('app-loading-spinner')).not.toBeNull();

    await fixture.whenStable();
    const request = httpMock.expectOne('/api/about');
    expect(request.request.method).toBe('GET');
    request.flush({
      applicationName: 'Demo Task Manager',
      description: 'Angular and ASP.NET Core demo.',
    });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="application-name"]').textContent).toContain(
      'Demo Task Manager',
    );
    expect(
      fixture.nativeElement.querySelector('[data-testid="application-description"]').textContent,
    ).toContain('Angular and ASP.NET Core demo.');
  });

  it('shows an error state and retries the request', async () => {
    await fixture.whenStable();
    httpMock.expectOne('/api/about').flush('server error', {
      status: 500,
      statusText: 'Internal Server Error',
    });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="about-error"]')).not.toBeNull();

    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();

    const retryRequest = httpMock.expectOne('/api/about');
    retryRequest.flush({ applicationName: 'Recovered', description: 'Available again.' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Recovered');
  });
});
