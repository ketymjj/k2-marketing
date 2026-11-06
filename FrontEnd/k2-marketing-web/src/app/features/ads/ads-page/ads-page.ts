import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { AD_STATUSES, AdStatus, Advertisement, CampaignLookup, ClientLookup, Paged } from '../../../shared/models/models';
import { PagedList, confirm, debounce } from '../../../shared/paged-list';
import { AdPreviewComponent } from '../ad-preview/ad-preview';
import { LinkCreativeDialog } from '../link-creative-dialog/link-creative-dialog';
import { openAdForm } from '../ad-shared';
import { AdCompareDialog } from '../ad-compare-dialog/ad-compare-dialog';

@Component({
  imports: [UI, MatAutocompleteModule, AdPreviewComponent],
  templateUrl: './ads-page.html',
  styleUrl: './ads-page.css'
})
export class AdsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  readonly auth = inject(AuthService);
  readonly statuses = AD_STATUSES;
  readonly clients = signal<ClientLookup[]>([]);
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly selected = signal<string[]>([]);
  readonly columns = ['select', 'name', 'campaign', 'creatives', 'status', 'spend', 'ctr', 'cpa', 'actions'];
  view: 'cards' | 'table' = 'cards';
  search = '';
  clientId: string | null = null;
  campaignId: string | null = null;
  status: AdStatus | null = null;
  readonly list = new PagedList<Advertisement>(p => this.api.get<Paged<Advertisement>>('/advertisements', p),
    () => ({ search: this.search, clientId: this.clientId, campaignId: this.campaignId, status: this.status }));
  readonly searchChanged = debounce(() => this.list.load(true));

  ngOnInit(): void {
    this.list.pageSize = 12;
    const qp = this.route.snapshot.queryParamMap;
    this.clientId = qp.get('clientId');
    this.campaignId = qp.get('campaignId');
    if (!this.auth.isClientViewer()) this.lookups.clients().subscribe(c => this.clients.set(c));
    this.loadCampaigns();
    this.list.load();
  }

  onClient(): void { this.campaignId = null; this.loadCampaigns(); this.list.load(true); }
  private loadCampaigns(): void { this.lookups.campaignsFor(this.clientId).subscribe(c => this.campaigns.set(c)); }

  isSelected(id: string): boolean { return this.selected().includes(id); }
  toggleSelect(id: string): void {
    this.selected.update(s => s.includes(id) ? s.filter(x => x !== id) : s.length < 4 ? [...s, id] : s);
  }

  openForm(id?: string): void {
    openAdForm(this.dialog, { id, campaignId: this.campaignId ?? undefined }).subscribe(a => a && this.list.load());
  }

  link(a: Advertisement): void {
    this.dialog.open(LinkCreativeDialog, { data: a, width: '880px', maxWidth: '96vw' }).afterClosed().subscribe(() => this.list.load());
  }

  duplicate(a: Advertisement): void {
    this.api.post(`/advertisements/${a.id}/duplicate`).subscribe(() => { this.toast.success('ads.duplicated'); this.list.load(); });
  }

  remove(a: Advertisement): void {
    confirm(this.dialog, { title: 'common.delete', message: 'ads.confirmDelete', params: { name: a.name }, danger: true, confirm: 'common.delete' })
      .subscribe(() => this.api.delete(`/advertisements/${a.id}`).subscribe(() => { this.toast.success('common.deleted'); this.list.load(); }));
  }

  compare(): void {
    this.api.post<Advertisement[]>('/advertisements/compare', { ids: this.selected() })
      .subscribe(ads => this.dialog.open(AdCompareDialog, { data: ads, width: '1100px', maxWidth: '96vw' }));
  }
}
