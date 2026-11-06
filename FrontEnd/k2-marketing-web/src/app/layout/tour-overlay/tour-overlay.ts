import { Component, HostListener, computed, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoModule } from '@jsverse/transloco';
import { TourService } from '../../core/services/tour.service';

interface Box { top: number; left: number; width: number; height: number; }

const CARD_W = 360;

/** Destaque + cartão explicativo do tour. Não captura cliques fora do cartão. */
@Component({
  selector: 'k2-tour-overlay',
  imports: [MatButtonModule, MatIconModule, TranslocoModule],
  templateUrl: './tour-overlay.html',
  styleUrl: './tour-overlay.css'
})
export class TourOverlayComponent {
  readonly tour = inject(TourService);
  readonly box = signal<Box | null>(null);
  private readonly viewport = signal({ w: window.innerWidth, h: window.innerHeight });

  readonly step = computed(() => this.tour.steps[this.tour.index()]);
  readonly isLast = computed(() => this.tour.index() === this.tour.steps.length - 1);

  /** Posição do cartão: abaixo do elemento, acima se não couber, ou centralizado sem destaque. */
  readonly cardStyle = computed(() => {
    const b = this.box();
    const { w, h } = this.viewport();
    if (!b || w < 640) return null;
    const left = Math.min(Math.max(12, b.left + b.width / 2 - CARD_W / 2), w - CARD_W - 12);
    const below = b.top + b.height + 16;
    return below + 260 < h ? { top: below, left } : { top: Math.max(12, b.top - 16 - 250), left };
  });

  constructor() {
    effect(() => {
      const el = this.tour.target();
      this.box.set(el ? this.measure(el) : null);
    });
  }

  @HostListener('window:resize')
  @HostListener('window:scroll')
  onViewportChange(): void {
    this.viewport.set({ w: window.innerWidth, h: window.innerHeight });
    const el = this.tour.target();
    if (el) this.box.set(this.measure(el));
  }

  @HostListener('document:keydown.escape')
  onEscape(): void { if (this.tour.active()) this.tour.skip(); }

  private measure(el: HTMLElement): Box {
    const r = el.getBoundingClientRect();
    return { top: r.top - 6, left: r.left - 6, width: r.width + 12, height: r.height + 12 };
  }
}
