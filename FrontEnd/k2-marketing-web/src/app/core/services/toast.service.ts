import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoService } from '@jsverse/transloco';

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly snack = inject(MatSnackBar);
  private readonly t = inject(TranslocoService);

  success(key: string, params?: Record<string, unknown>): void {
    this.open(this.t.translate(key, params), 'toast-success');
  }

  error(key: string, params?: Record<string, unknown>): void {
    this.open(this.t.translate(key, params), 'toast-error', 6000);
  }

  info(key: string, params?: Record<string, unknown>): void {
    this.open(this.t.translate(key, params), 'toast-info');
  }

  /** Traduz o código de erro da API (ex.: campaign.invalidTransition); sem tradução, usa a mensagem do servidor. */
  apiError(code?: string, fallback?: string): void {
    const key = `apiErrors.${code}`;
    const translated = code ? this.t.translate(key) : key;
    this.open(translated !== key ? translated : (fallback ?? this.t.translate('errors.generic')), 'toast-error', 6000);
  }

  private open(message: string, panelClass: string, duration = 3500): void {
    this.snack.open(message, '✕', { duration, panelClass, horizontalPosition: 'right', verticalPosition: 'top' });
  }
}
