import { Component, computed, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { TranslocoModule } from '@jsverse/transloco';
import '../analytics.shared';
import { LearningService } from '../../../core/services/learning.service';
import { metricFromLabel } from '../../../features/learning/learning.shared';
import { openMetricHelp } from '../metric-help-dialog/metric-help-dialog';

/** Card de indicador com variação em relação ao período anterior. */
@Component({
  selector: 'k2-kpi',
  imports: [CommonModule, MatIconModule, MatTooltipModule, TranslocoModule],
  templateUrl: './kpi.html',
  styleUrl: './kpi.css'
})
export class KpiComponent {
  private readonly dialog = inject(MatDialog);
  readonly learning = inject(LearningService);
  readonly label = input.required<string>();
  readonly value = input.required<string | number>();
  readonly icon = input('insights');
  readonly hint = input<string>();
  readonly caption = input<string>();
  readonly change = input<number | null>();
  /** Para custos (CPC, CPA...), queda é positiva. */
  readonly lowerIsBetter = input(false);
  readonly tone = input<'purple' | 'blue' | 'green' | 'orange' | 'navy' | 'pink' | 'teal' | 'red'>('purple');
  readonly good = computed(() => (this.change() ?? 0) >= 0 ? !this.lowerIsBetter() : this.lowerIsBetter());
  /** Indicador com explicação na Central de Aprendizado (CTR, CPC, ROAS...). */
  readonly metric = computed(() => metricFromLabel(this.label()));

  openHelp(): void {
    const m = this.metric();
    if (m) openMetricHelp(this.dialog, m);
  }
}
