import { Component, computed, inject, input } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoModule } from '@jsverse/transloco';
import { LanguageService } from '../../../core/i18n/i18n';
import { openMetricHelp } from '../../../shared/components/metric-help-dialog/metric-help-dialog';
import { MetricInputs, calculate } from '../learning.shared';

interface ResultCard { key: string; value: string; expression: string; empty: boolean; }

/** Cards com os indicadores calculados e a conta feita com os números informados. */
@Component({
  selector: 'k2-calc-results',
  imports: [MatIconModule, MatTooltipModule, TranslocoModule],
  templateUrl: './calc-results.html',
  styleUrl: './calc-results.css'
})
export class CalcResultsComponent {
  private readonly dialog = inject(MatDialog);
  private readonly lang = inject(LanguageService);
  readonly inputs = input.required<MetricInputs>();

  readonly cards = computed<ResultCard[]>(() => {
    const m = this.inputs();
    const c = calculate(m);
    const locale = this.lang.lang();
    const num = (v: number, d = 0) => new Intl.NumberFormat(locale, { minimumFractionDigits: d, maximumFractionDigits: d }).format(v);
    const money = (v: number) => new Intl.NumberFormat(locale, { style: 'currency', currency: 'BRL' }).format(v);
    const dash = '—';
    return [
      { key: 'ctr', value: c.ctr === null ? dash : `${num(c.ctr, 2)}%`, expression: `${num(m.clicks)} ÷ ${num(m.impressions)} × 100`, empty: c.ctr === null },
      { key: 'cpc', value: c.cpc === null ? dash : money(c.cpc), expression: `${money(m.spend)} ÷ ${num(m.clicks)}`, empty: c.cpc === null },
      { key: 'cpm', value: c.cpm === null ? dash : money(c.cpm), expression: `${money(m.spend)} ÷ ${num(m.impressions)} × 1.000`, empty: c.cpm === null },
      { key: 'cpl', value: c.cpl === null ? dash : money(c.cpl), expression: `${money(m.spend)} ÷ ${num(m.leads)}`, empty: c.cpl === null },
      { key: 'cpa', value: c.cpa === null ? dash : money(c.cpa), expression: `${money(m.spend)} ÷ ${num(m.conversions)}`, empty: c.cpa === null },
      { key: 'roas', value: c.roas === null ? dash : `${num(c.roas, 2)}x`, expression: `${money(m.revenue)} ÷ ${money(m.spend)}`, empty: c.roas === null }
    ];
  });

  help(key: string): void { openMetricHelp(this.dialog, key); }
}
