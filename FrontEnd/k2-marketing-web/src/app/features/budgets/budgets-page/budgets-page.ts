import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { LanguageService } from '../../../core/i18n/i18n';
import { AnalyticsFilter, PALETTE } from '../../../shared/components/analytics.shared';
import { ChartComponent } from '../../../shared/components/chart/chart';
import { FilterBarComponent } from '../../../shared/components/filter-bar/filter-bar';
import { KpiComponent } from '../../../shared/components/kpi/kpi';
import { UsageBarComponent } from '../../../shared/components/usage-bar/usage-bar';
import { Budget, BudgetSummary } from '../../../shared/models/models';
import { MoneyPipe, PctPipe } from '../../../shared/pipes/format.pipes';
import { barConfig, moneyTick } from '../../../shared/chart-helpers';
import { confirm } from '../../../shared/paged-list';
import { BudgetDetailDialog } from '../budget-detail-dialog/budget-detail-dialog';
import { SpendDialog } from '../spend-dialog/spend-dialog';
import { openBudgetForm } from '../budget-shared';

@Component({
  imports: [UI, KpiComponent, ChartComponent, FilterBarComponent, UsageBarComponent],
  providers: [MoneyPipe, PctPipe],
  templateUrl: './budgets-page.html',
  styleUrl: './budgets-page.css'
})
export class BudgetsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly t = inject(TranslocoService);
  private readonly lang = inject(LanguageService);
  readonly auth = inject(AuthService);
  readonly money = inject(MoneyPipe);
  readonly pct = inject(PctPipe);
  readonly s = signal<BudgetSummary | null>(null);
  readonly levelSig = signal(0);
  get level(): number { return this.levelSig(); }
  set level(v: number) { this.levelSig.set(v); }
  initial: Partial<AnalyticsFilter> | null = null;
  private filter: AnalyticsFilter | null = null;

  readonly rows = computed(() => (this.s()?.items ?? []).filter(b => b.threshold >= this.levelSig()));
  readonly chart = computed(() => {
    const items = [...this.s()!.items].sort((a, b) => b.plannedAmount - a.plannedAmount).slice(0, 12);
    this.lang.lang();
    return barConfig(items.map(i => i.campaignName.length > 22 ? i.campaignName.slice(0, 21) + '…' : i.campaignName), [
      { label: this.t.translate('budgets.planned'), data: items.map(i => i.plannedAmount), color: PALETTE.gray + 'aa' },
      { label: this.t.translate('budgets.spent'), data: items.map(i => i.actualSpend), color: items.map(i => i.threshold >= 100 ? PALETTE.red : i.threshold >= 80 ? PALETTE.orange : PALETTE.purple) }
    ], { scales: { x: { grid: { display: false } }, y: { ticks: { callback: moneyTick(this.lang.lang()) } } } });
  });

  ngOnInit(): void {
    const campaignId = this.route.snapshot.queryParamMap.get('campaignId');
    if (campaignId) this.initial = { campaignId };
  }

  load(f: AnalyticsFilter): void {
    this.filter = f;
    this.api.get<BudgetSummary>('/budgets/summary', { clientId: f.clientId, campaignId: f.campaignId, from: f.from, to: f.to }).subscribe(s => this.s.set(s));
  }

  reload(): void { if (this.filter) this.load(this.filter); }
  create(): void { openBudgetForm(this.dialog, { campaignId: this.filter?.campaignId ?? undefined }).subscribe(b => b && this.reload()); }
  edit(b: Budget): void { openBudgetForm(this.dialog, { budget: b }).subscribe(r => r && this.reload()); }
  spend(b: Budget): void { this.dialog.open(SpendDialog, { data: b, width: '520px', maxWidth: '95vw' }).afterClosed().subscribe(r => r && this.reload()); }
  history(b: Budget): void { this.dialog.open(BudgetDetailDialog, { data: b, width: '860px', maxWidth: '96vw' }); }
  remove(b: Budget): void {
    confirm(this.dialog, { title: 'common.delete', message: 'budgets.confirmDelete', params: { name: b.campaignName }, danger: true, confirm: 'common.delete' })
      .subscribe(() => this.api.delete(`/budgets/${b.id}`).subscribe(() => { this.toast.success('common.deleted'); this.reload(); }));
  }
}
