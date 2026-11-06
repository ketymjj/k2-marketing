import { Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';
const KEY = 'k2m.theme';

/** Tema claro/escuro com persistência; o padrão segue a preferência do sistema operacional. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<Theme>(this.initial());

  init(): void {
    this.apply(this.theme());
  }

  toggle(): void {
    this.set(this.theme() === 'dark' ? 'light' : 'dark');
  }

  set(theme: Theme): void {
    this.theme.set(theme);
    try { localStorage.setItem(KEY, theme); } catch { /* ignore */ }
    this.apply(theme);
  }

  private apply(theme: Theme): void {
    document.documentElement.classList.toggle('dark', theme === 'dark');
    document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme === 'dark' ? '#0c111d' : '#101828');
  }

  private initial(): Theme {
    try {
      const v = localStorage.getItem(KEY);
      if (v === 'dark' || v === 'light') return v;
      return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    } catch {
      return 'light';
    }
  }
}
