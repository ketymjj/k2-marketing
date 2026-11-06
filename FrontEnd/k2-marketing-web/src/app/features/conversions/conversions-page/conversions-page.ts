import { Component, OnInit, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService, isoDate } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { KpiComponent } from '../../../shared/components/kpi/kpi';
import { CONVERSION_TYPES, CampaignLookup, ClientLookup, Conversion, ConversionSummary, ConversionType, DATA_SOURCES, DataSource, Paged } from '../../../shared/models/models';
import { MoneyPipe, NumPipe } from '../../../shared/pipes/format.pipes';
import { PagedList, confirm, debounce } from '../../../shared/paged-list';
import { openConversionForm } from '../conversion-form';

@Component({
  imports: [UI, KpiComponent],
  providers: [MoneyPipe, NumPipe],
  templateUrl: './conversions-page.html',
  styleUrl: './conversions-page.css'
})
export class ConversionsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  readonly auth = inject(AuthService);
  readonly money = inject(MoneyPipe);
  readonly num = inject(NumPipe);
  readonly types = CONVERSION_TYPES;
  readonly sources = DATA_SOURCES;
  readonly clients = signal<ClientLookup[]>([]);
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly summary = signal<ConversionSummary | null>(null);
  readonly columns = ['conversionDate', 'type', 'campaign', 'lead', 'externalId', 'source', 'revenueAmount', 'actions'];
  search = '';
  clientId: string | null = null;
  campaignId: string | null = null;
  type: ConversionType | null = null;
  source: DataSource | null = null;
  from: Date | null = null;
  to: Date | null = null;
  private filters = () => ({ search: this.search, clientId: this.clientId, campaignId: this.campaignId, type: this.type, source: this.source, from: isoDate(this.from), to: isoDate(this.to) });
  readonly list = new PagedList<Conversion>(p => this.api.get<Paged<Conversion>>('/conversions', p), this.filters);
  readonly searchChanged = debounce(() => this.reload());

  ngOnInit(): void {
    if (!this.auth.isClientViewer()) this.lookups.clients().subscribe(c => this.clients.set(c));
    this.lookups.campaignsFor(null).subscribe(c => this.campaigns.set(c));
    this.reload();
  }

  onClient(): void {
    this.campaignId = null;
    this.lookups.campaignsFor(this.clientId).subscribe(c => this.campaigns.set(c));
    this.reload();
  }

  reload(): void {
    this.list.load(true);
    this.api.get<ConversionSummary>('/conversions/summary', this.filters()).subscribe(s => this.summary.set(s));
  }

  create(): void { openConversionForm(this.dialog, { campaignId: this.campaignId ?? undefined, clientId: this.clientId ?? undefined }).subscribe(r => r && this.reload()); }

  remove(c: Conversion): void {
    confirm(this.dialog, { title: 'common.delete', message: 'conversions.confirmDelete', danger: true, confirm: 'common.delete' })
      .subscribe(() => this.api.delete(`/conversions/${c.id}`).subscribe(() => { this.toast.success('common.deleted'); this.reload(); }));
  }
}
