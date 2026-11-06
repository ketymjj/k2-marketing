import { Injectable, signal } from '@angular/core';

const KEY = 'k2m.beginner';

/**
 * Modo Iniciante: exibe explicações adicionais (dashboards, gráficos, formulários e relatórios).
 * Desligado, a interface volta ao modo profissional; os ícones de ajuda continuam disponíveis.
 */
@Injectable({ providedIn: 'root' })
export class LearningService {
  readonly beginner = signal<boolean>(this.read());

  setBeginner(on: boolean): void {
    this.beginner.set(on);
    try { localStorage.setItem(KEY, on ? '1' : '0'); } catch { /* armazenamento indisponível */ }
  }

  toggle(): void { this.setBeginner(!this.beginner()); }

  private read(): boolean {
    try { return localStorage.getItem(KEY) === '1'; } catch { return false; }
  }
}
