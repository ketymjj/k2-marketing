import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { UI } from '../../../shared/ui';
import { ApiService, isoDate } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { BoardColumn, CampaignLookup, ClientLookup, LEAD_STATUSES, Lead, LeadStatus, Paged } from '../../../shared/models/models';
import { PagedList, debounce } from '../../../shared/paged-list';
import { LeadDetailDialog } from '../lead-detail-dialog/lead-detail-dialog';
import { LeadFormDialog } from '../lead-form-dialog/lead-form-dialog';

@Component({
  imports: [UI, DragDropModule],
  templateUrl: './leads-page.html',
  styleUrl: './leads-page.css'
})
export class LeadsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);
  readonly statuses = LEAD_STATUSES;
  readonly clients = signal<ClientLookup[]>([]);
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly sources = signal<string[]>([]);
  readonly board = signal<BoardColumn[]>([]);
  readonly columns = ['name', 'client', 'campaign', 'source', 'status', 'createdAt'];
  view: 'board' | 'table' = 'board';
  search = '';
  clientId: string | null = null;
  campaignId: string | null = null;
  source: string | null = null;
  status: LeadStatus | null = null;
  from: Date | null = null;
  to: Date | null = null;
  private filters = () => ({ search: this.search, clientId: this.clientId, campaignId: this.campaignId, source: this.source, from: isoDate(this.from), to: isoDate(this.to) });
  readonly list = new PagedList<Lead>(p => this.api.get<Paged<Lead>>('/leads', p), () => ({ ...this.filters(), status: this.status }));
  readonly searchChanged = debounce(() => this.reload());

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.clientId = qp.get('clientId');
    if (!this.auth.isClientViewer()) this.lookups.clients().subscribe(c => this.clients.set(c));
    this.lookups.campaignsFor(this.clientId).subscribe(c => this.campaigns.set(c));
    this.api.get<string[]>('/leads/sources').subscribe(s => this.sources.set(s));
    this.reload();
    const leadId = qp.get('leadId');
    if (leadId) {
      this.open(leadId);
      this.router.navigate([], { queryParams: { leadId: null }, queryParamsHandling: 'merge', replaceUrl: true });
    }
  }

  onClient(): void {
    this.campaignId = null;
    this.lookups.campaignsFor(this.clientId).subscribe(c => this.campaigns.set(c));
    this.reload();
  }

  reload(): void {
    if (this.view === 'board') this.api.get<BoardColumn[]>('/leads/board', { ...this.filters(), perColumn: 50 }).subscribe(b => this.board.set(b));
    else this.list.load(true);
  }

  /** Arrastar um card para outra coluna altera o status do lead (registrado no histórico). */
  drop(e: CdkDragDrop<BoardColumn>): void {
    if (e.previousContainer === e.container) return;
    const lead = e.item.data as Lead;
    const target = e.container.data.status;
    // Atualização otimista; desfeita em caso de erro.
    const previous = this.board();
    this.board.set(previous.map(col => col.status === lead.status
      ? { ...col, count: col.count - 1, items: col.items.filter(i => i.id !== lead.id) }
      : col.status === target ? { ...col, count: col.count + 1, items: [{ ...lead, status: target }, ...col.items] } : col));
    this.api.patch<Lead>(`/leads/${lead.id}/status`, { status: target }).subscribe({
      next: () => this.toast.success('leads.statusChanged'),
      error: () => this.board.set(previous)
    });
  }

  open(id: string): void {
    this.dialog.open(LeadDetailDialog, { data: { id }, width: '720px', maxWidth: '96vw' }).afterClosed().subscribe(changed => changed && this.reload());
  }

  create(): void {
    this.dialog.open(LeadFormDialog, { data: { clientId: this.clientId }, width: '640px', maxWidth: '95vw' }).afterClosed().subscribe(r => r && this.reload());
  }
}
