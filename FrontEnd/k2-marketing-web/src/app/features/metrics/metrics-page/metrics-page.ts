import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService, isoDate } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { CampaignLookup, ClientLookup, DATA_SOURCES, DataSource, MetricRow, Metrics, Paged } from '../../../shared/models/models';
import { PagedList, confirm } from '../../../shared/paged-list';
import { MetricFormDialog } from '../metric-form-dialog/metric-form-dialog';

@Component({
  imports: [UI],
  templateUrl: './metrics-page.html',
  styleUrl: './metrics-page.css'
})
export class MetricsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);
  readonly sources = DATA_SOURCES;
  readonly clients = signal<ClientLookup[]>([]);
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly totals = signal<Metrics | null>(null);
  readonly columns = ['date', 'campaign', 'impressions', 'clicks', 'spend', 'leads', 'conversions', 'revenue', 'ctr', 'cpc', 'roas', 'source', 'actions'];
  clientId: string | null = null;
  campaignId: string | null = null;
  source: DataSource | null = null;
  from: Date | null = null;
  to: Date | null = null;
  private filters = () => ({ clientId: this.clientId, campaignId: this.campaignId, source: this.source, from: isoDate(this.from), to: isoDate(this.to) });
  readonly list = new PagedList<MetricRow>(p => this.api.get<Paged<MetricRow>>('/metrics', p), this.filters);

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.campaignId = qp.get('campaignId');
    if (!this.auth.isClientViewer()) this.lookups.clients().subscribe(c => this.clients.set(c));
    this.lookups.campaignsFor(null).subscribe(c => this.campaigns.set(c));
    this.reload();
    if (qp.get('new') && this.auth.canOperate()) {
      this.openForm();
      this.router.navigate([], { queryParams: { new: null }, queryParamsHandling: 'merge', replaceUrl: true });
    }
  }

  onClient(): void {
    this.campaignId = null;
    this.lookups.campaignsFor(this.clientId).subscribe(c => this.campaigns.set(c));
    this.reload();
  }

  reload(): void {
    this.list.load(true);
    this.api.get<Metrics>('/metrics/totals', this.filters()).subscribe(t => this.totals.set(t));
  }

  openForm(row?: MetricRow): void {
    this.dialog.open(MetricFormDialog, { data: { row, campaignId: this.campaignId }, width: '760px', maxWidth: '96vw' }).afterClosed().subscribe(r => r && this.reload());
  }

  remove(m: MetricRow): void {
    confirm(this.dialog, { title: 'common.delete', message: 'metrics.confirmDelete', params: { date: m.date }, danger: true, confirm: 'common.delete' })
      .subscribe(() => this.api.delete(`/metrics/${m.id}`).subscribe(() => { this.toast.success('common.deleted'); this.reload(); }));
  }
}
