import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { ToastService } from '../services/toast.service';
import { ApiProblem } from '../../shared/models/models';

const withToken = (req: HttpRequest<unknown>, token: string | null) =>
  token && req.url.startsWith('/api/') ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

const isAuthCall = (url: string) => /\/auth\/(login|refresh|logout)/.test(url);

/** Anexa o JWT e, em caso de 401, tenta renovar a sessão uma única vez. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  return next(withToken(req, auth.accessToken)).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || isAuthCall(req.url) || !auth.isAuthenticated()) return throwError(() => error);
      return auth.refresh().pipe(
        catchError(refreshError => {
          auth.logout();
          return throwError(() => refreshError);
        }),
        switchMap(token => next(withToken(req, token))));
    }));
};

/** Exibe mensagens de erro traduzidas a partir do código retornado pela API. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const silent = req.headers.has('X-Silent') || (error.status === 401 && !isAuthCall(req.url));
      if (silent) return throwError(() => error);
      // Respostas de erro em blob (downloads) precisam ser lidas como texto.
      const problem$ = error.error instanceof Blob
        ? from(error.error.text().then(t => { try { return JSON.parse(t) as ApiProblem; } catch { return null; } }))
        : from(Promise.resolve(error.error as ApiProblem | null));
      problem$.subscribe(problem => {
        if (error.status === 0) toast.error('errors.network');
        else if (error.status === 429) toast.error('errors.tooManyRequests');
        else if (problem?.code === 'validation' && problem.errors) {
          toast.error('errors.validation', { details: Object.values(problem.errors).flat().join(' ') });
        } else toast.apiError(problem?.code, problem?.title);
      });
      return throwError(() => error);
    }));
};
