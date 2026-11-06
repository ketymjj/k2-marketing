import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { Advertisement } from '../../../shared/models/models';
import { AdPreviewComponent } from '../ad-preview/ad-preview';

/** Comparação de anúncios: o melhor valor de cada métrica (entre anúncios com cliques) é destacado. */
@Component({
  imports: [UI, AdPreviewComponent],
  templateUrl: './ad-compare-dialog.html',
  styleUrl: './ad-compare-dialog.css'
})
export class AdCompareDialog {
  readonly metrics = [
    { key: 'spend', fmt: 'money', better: 0 }, { key: 'impressions', fmt: 'num', better: 0 }, { key: 'clicks', fmt: 'num', better: 1 },
    { key: 'ctr', fmt: 'pct', better: 1 }, { key: 'cpc', fmt: 'money', better: -1 }, { key: 'conversions', fmt: 'num', better: 1 },
    { key: 'cpa', fmt: 'money', better: -1 }, { key: 'roas', fmt: 'roas', better: 1 }
  ];
  constructor(@Inject(MAT_DIALOG_DATA) public data: Advertisement[]) {}

  val(a: Advertisement, key: string): number | null {
    return (a.metrics as unknown as Record<string, number | null>)[key === 'revenue' ? 'attributedRevenue' : key];
  }

  isBest(key: string, a: Advertisement): boolean {
    const m = this.metrics.find(x => x.key === key)!;
    if (!m.better) return false;
    const values = this.data.filter(x => x.metrics.clicks > 0).map(x => this.val(x, key)).filter((v): v is number => v !== null);
    const v = this.val(a, key);
    if (v === null || values.length < 2 || a.metrics.clicks === 0) return false;
    const best = m.better > 0 ? Math.max(...values) : Math.min(...values);
    return v === best && values.filter(x => x === best).length === 1;
  }
}
