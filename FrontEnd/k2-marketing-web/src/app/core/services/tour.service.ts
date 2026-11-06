import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

export interface TourStep { key: string; route: string; target: string; }

const DONE_KEY = 'k2m.tourDone';

/** Etapas do tour: cada uma abre a tela real e destaca o elemento marcado com data-tour. */
export const TOUR_STEPS: TourStep[] = [
  { key: 'dashboard', route: '/dashboard', target: '[data-tour=kpis]' },
  { key: 'client', route: '/clients', target: '[data-tour=new-client]' },
  { key: 'campaign', route: '/campaigns', target: '[data-tour=new-campaign]' },
  { key: 'budget', route: '/budgets', target: '[data-tour=new-budget]' },
  { key: 'ad', route: '/ads', target: '[data-tour=new-ad]' },
  { key: 'creative', route: '/creatives', target: '[data-tour=upload-creative]' },
  { key: 'results', route: '/metrics', target: '[data-tour=metrics-actions]' },
  { key: 'metrics', route: '/dashboard', target: '[data-tour=kpi-help]' },
  { key: 'lead', route: '/leads', target: '[data-tour=new-lead]' },
  { key: 'report', route: '/reports', target: '[data-tour=export-pdf]' }
];

/**
 * Tutorial guiado sem bloqueio: o destaque é apenas visual (pointer-events: none), então o usuário
 * pode usar a tela normalmente durante o tour. Pode ser reiniciado a qualquer momento.
 */
@Injectable({ providedIn: 'root' })
export class TourService {
  private readonly router = inject(Router);
  readonly steps = TOUR_STEPS;
  readonly active = signal(false);
  readonly index = signal(0);
  /** Elemento destacado na etapa atual (null quando não existe para o perfil do usuário). */
  readonly target = signal<HTMLElement | null>(null);
  readonly locating = signal(false);
  private run = 0;

  get done(): boolean {
    try { return localStorage.getItem(DONE_KEY) === '1'; } catch { return false; }
  }

  start(): void {
    this.active.set(true);
    this.go(0);
  }

  next(): void { if (this.index() < this.steps.length - 1) this.go(this.index() + 1); else this.finish(); }
  prev(): void { if (this.index() > 0) this.go(this.index() - 1); }

  skip(): void { this.close(); }

  finish(): void {
    try { localStorage.setItem(DONE_KEY, '1'); } catch { /* ignore */ }
    this.close();
  }

  private close(): void {
    this.run++;
    this.active.set(false);
    this.target.set(null);
  }

  private go(i: number): void {
    const step = this.steps[i];
    const run = ++this.run;
    this.index.set(i);
    this.target.set(null);
    this.locating.set(true);
    this.router.navigateByUrl(step.route).then(() => this.locate(step, run));
  }

  /** Aguarda o elemento aparecer (telas carregam dados de forma assíncrona) por até ~4 s. */
  private locate(step: TourStep, run: number, attempt = 0): void {
    if (run !== this.run) return;
    const el = document.querySelector<HTMLElement>(step.target);
    if (el && el.getBoundingClientRect().width > 0) {
      el.scrollIntoView({ block: 'center', behavior: 'smooth' });
      setTimeout(() => { if (run === this.run) { this.target.set(el); this.locating.set(false); } }, 350);
      return;
    }
    if (attempt >= 40) { this.locating.set(false); return; }
    setTimeout(() => this.locate(step, run, attempt + 1), 100);
  }
}
