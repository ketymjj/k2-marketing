import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService, isoDate } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { AdPlatform, CAMPAIGN_STATUSES, CampaignListItem, CampaignObjective, CampaignStatus, ClientLookup, OBJECTIVES, PLATFORMS, Paged } from '../../../shared/models/models';
import { PagedList, confirm, debounce } from '../../../shared/paged-list';
import { STATUS_ACTION, TRANSITIONS, changeCampaignStatus, openCampaignForm } from '../campaign-shared';

@Component({
  imports: [UI],
  templateUrl: './campaigns-page.html',
  styleUrl: './campaigns-page.css'
})
export class CampaignsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);
  readonly platforms = PLATFORMS;
  readonly statuses = CAMPAIGN_STATUSES;
  readonly objectives = OBJECTIVES;
  readonly clients = signal<ClientLookup[]>([]);
  readonly columns = ['name', 'platform', 'objective', 'status', 'startDate', 'plannedBudget', 'spend', 'roas', 'actions'];
  search = '';
  clientId: string | null = null;
  platform: AdPlatform | null = null;
  status: CampaignStatus | null = null;
  objective: CampaignObjective | null = null;
  from: Date | null = null;
  to: Date | null = null;
  readonly list = new PagedList<CampaignListItem>(p => this.api.get<Paged<CampaignListItem>>('/campaigns', p), () => ({
    search: this.search, clientId: this.clientId, platform: this.platform, status: this.status, objective: this.objective,
    from: isoDate(this.from), to: isoDate(this.to)
  }));
  readonly searchChanged = debounce(() => this.list.load(true));

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.clientId = qp.get('clientId');
    this.status = qp.get('status') as CampaignStatus | null;
    if (!this.auth.isClientViewer()) this.lookups.clients().subscribe(c => this.clients.set(c));
    this.list.load();
    if (qp.get('new') && this.auth.canOperate()) {
      this.openForm(undefined, this.clientId ?? undefined);
      this.router.navigate([], { queryParams: { new: null }, queryParamsHandling: 'merge', replaceUrl: true });
    }
  }

  transitions(s: CampaignStatus): CampaignStatus[] { return TRANSITIONS[s]; }
  action(s: CampaignStatus) { return STATUS_ACTION[s]; }

  openForm(id?: string, clientId?: string): void {
    openCampaignForm(this.dialog, { id, clientId }).subscribe(c => {
      if (!c) return;
      if (!id) this.router.navigate(['/campaigns', c.id]);
      else this.list.load();
    });
  }

  duplicate(c: CampaignListItem): void {
    confirm(this.dialog, { title: 'common.duplicate', message: 'campaigns.confirmDuplicate', params: { name: c.name } })
      .subscribe(() => this.api.post(`/campaigns/${c.id}/duplicate`).subscribe(() => { this.toast.success('campaigns.duplicated'); this.list.load(); }));
  }

  setStatus(c: CampaignListItem, s: CampaignStatus): void {
    changeCampaignStatus(this.dialog, this.api, this.toast, c, s).subscribe(() => this.list.load());
  }

  remove(c: CampaignListItem): void {
    confirm(this.dialog, { title: 'common.delete', message: 'campaigns.confirmDelete', params: { name: c.name }, danger: true, confirm: 'common.delete' })
      .subscribe(() => this.api.delete(`/campaigns/${c.id}`).subscribe(() => { this.toast.success('common.deleted'); this.lookups.invalidate(); this.list.load(); }));
  }
}
