import { Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import '../analytics.shared';

/** Barra de progresso de consumo de orçamento com cores por limite (80/90/100%). */
@Component({
  selector: 'k2-usage-bar',
  imports: [CommonModule],
  templateUrl: './usage-bar.html',
  styleUrl: './usage-bar.css'
})
export class UsageBarComponent {
  readonly value = input<number | null>(null);
  readonly width = computed(() => Math.min(100, Math.max(0, this.value() ?? 0)));
  readonly level = computed(() => {
    const v = this.value() ?? 0;
    return v >= 100 ? 'lvl-100' : v >= 90 ? 'lvl-90' : v >= 80 ? 'lvl-80' : 'lvl-ok';
  });
}
