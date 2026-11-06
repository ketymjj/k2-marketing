import { Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { UI } from '../../../shared/ui';
import { AuthService } from '../../../core/auth/auth.service';
import { AppLang, LanguageService } from '../../../core/i18n/i18n';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  imports: [UI],
  templateUrl: './login-page.html',
  styleUrl: './login-page.css'
})
export class LoginPage {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly lang = inject(LanguageService);
  readonly theme = inject(ThemeService);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly show = signal(false);
  readonly form = this.fb.nonNullable.group({ email: ['', [Validators.required, Validators.email]], password: ['', Validators.required] });

  /** Contas fictícias criadas pelo seed de demonstração. */
  readonly demo = [
    { role: 'Admin', email: 'admin@k2digital.com.br', password: 'Admin@123' },
    { role: 'Manager', email: 'gerente@k2digital.com.br', password: 'Gerente@123' },
    { role: 'Analyst', email: 'analista@k2digital.com.br', password: 'Analista@123' },
    { role: 'ClientViewer', email: 'cliente@bellamoda.com.br', password: 'Cliente@123' }
  ];

  setLang(l: AppLang): void { this.lang.set(l); }

  fill(email: string, password: string): void {
    this.form.setValue({ email, password });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: e => {
        this.loading.set(false);
        this.error.set(e.status === 429 ? 'errors.tooManyRequests' : e.status === 401 ? 'auth.invalid' : 'errors.generic');
      }
    });
  }
}
