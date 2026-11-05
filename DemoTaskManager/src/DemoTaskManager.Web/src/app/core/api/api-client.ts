import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_URL_PREFIX } from '../interceptors/api-base-url.interceptor';

export type QueryParamValue = string | number | boolean | null | undefined;

/**
 * Thin, strongly typed wrapper over HttpClient. Every URL goes through the
 * `api://` marker so the base URL stays runtime-configurable.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  get<TResponse>(path: string, query?: Readonly<Record<string, QueryParamValue>>): Observable<TResponse> {
    return this.http.get<TResponse>(this.url(path), { params: toHttpParams(query) });
  }

  post<TResponse, TBody = unknown>(path: string, body: TBody): Observable<TResponse> {
    return this.http.post<TResponse>(this.url(path), body);
  }

  put<TResponse, TBody = unknown>(path: string, body: TBody): Observable<TResponse> {
    return this.http.put<TResponse>(this.url(path), body);
  }

  delete<TResponse = void>(path: string): Observable<TResponse> {
    return this.http.delete<TResponse>(this.url(path));
  }

  private url(path: string): string {
    return `${API_URL_PREFIX}${path.replace(/^\/+/, '')}`;
  }
}

function toHttpParams(query?: Readonly<Record<string, QueryParamValue>>): HttpParams {
  let params = new HttpParams();
  if (!query) {
    return params;
  }

  for (const [key, value] of Object.entries(query)) {
    if (value !== null && value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }

  return params;
}
