import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

export const API_URL = '/api/v1';

export type Params = Record<string, string | number | boolean | null | undefined>;

/** Cliente HTTP fino para a API v1. Parâmetros nulos/vazios são omitidos. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  params(p?: Params): HttpParams {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(p ?? {})) {
      if (v !== null && v !== undefined && v !== '') params = params.set(k, String(v));
    }
    return params;
  }

  get<T>(path: string, params?: Params): Observable<T> {
    return this.http.get<T>(API_URL + path, { params: this.params(params) });
  }

  post<T>(path: string, body: unknown = {}): Observable<T> {
    return this.http.post<T>(API_URL + path, body);
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(API_URL + path, body);
  }

  patch<T>(path: string, body: unknown): Observable<T> {
    return this.http.patch<T>(API_URL + path, body);
  }

  delete<T = void>(path: string): Observable<T> {
    return this.http.delete<T>(API_URL + path);
  }

  /** Baixa um arquivo autenticado (CSV, criativos) e dispara o download no navegador. */
  download(path: string, params?: Params, fallbackName = 'arquivo'): Observable<void> {
    return this.http.get(API_URL + path, { params: this.params(params), responseType: 'blob', observe: 'response' }).pipe(
      map((res: HttpResponse<Blob>) => {
        const disposition = res.headers.get('Content-Disposition') ?? '';
        const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
        saveBlob(res.body!, match ? decodeURIComponent(match[1]) : fallbackName);
      }));
  }

  blob(path: string): Observable<Blob> {
    return this.http.get(API_URL + path, { responseType: 'blob' });
  }
}

export function saveBlob(blob: Blob, name: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** Data local no formato ISO (yyyy-MM-dd), sem conversão para UTC. */
export function isoDate(d: Date | null | undefined): string | null {
  if (!d) return null;
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

export function parseIsoDate(s: string): Date {
  const [y, m, d] = s.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d);
}

export function addDays(d: Date, days: number): Date {
  const r = new Date(d);
  r.setDate(r.getDate() + days);
  return r;
}
