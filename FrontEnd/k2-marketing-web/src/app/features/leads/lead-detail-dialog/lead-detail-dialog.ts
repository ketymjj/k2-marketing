import { Component, OnInit, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { INTERACTION_TYPES, LEAD_STATUSES, Lead, LeadDetail, LeadInteraction, LeadInteractionType, LeadStatus } from '../../../shared/models/models';
import { confirm } from '../../../shared/paged-list';
import { openConversionForm } from '../../conversions/conversion-form';
import { LeadFormDialog } from '../lead-form-dialog/lead-form-dialog';

@Component({
  imports: [UI],
  templateUrl: './lead-detail-dialog.html',
  styleUrl: './lead-detail-dialog.css'
})
export class LeadDetailDialog implements OnInit {
  readonly data = inject<{ id: string }>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly dialog = inject(MatDialog);
  readonly auth = inject(AuthService);
  readonly statuses = LEAD_STATUSES;
  readonly types = INTERACTION_TYPES;
  readonly d = signal<LeadDetail | null>(null);
  type: LeadInteractionType = 'Call';
  notes = '';
  changed = false;

  constructor(private ref: MatDialogRef<LeadDetailDialog>) {}

  ngOnInit(): void { this.load(); }
  load(): void { this.api.get<LeadDetail>(`/leads/${this.data.id}`).subscribe(d => this.d.set(d)); }

  setStatus(status: LeadStatus): void {
    this.api.patch<Lead>(`/leads/${this.data.id}/status`, { status }).subscribe(() => { this.changed = true; this.toast.success('leads.statusChanged'); this.load(); });
  }

  addInteraction(): void {
    if (!this.notes.trim()) return;
    this.api.post<LeadInteraction>(`/leads/${this.data.id}/interactions`, { type: this.type, notes: this.notes }).subscribe(() => { this.notes = ''; this.changed = true; this.load(); });
  }

  edit(): void {
    this.dialog.open(LeadFormDialog, { data: { lead: this.d()!.lead }, width: '640px', maxWidth: '95vw' }).afterClosed().subscribe(r => { if (r) { this.changed = true; this.load(); } });
  }

  conversion(): void {
    const l = this.d()!.lead;
    openConversionForm(this.dialog, { campaignId: l.campaignId!, clientId: l.clientId, leadId: l.id }).subscribe(r => { if (r) { this.changed = true; this.load(); } });
  }

  remove(): void {
    confirm(this.dialog, { title: 'leads.deleteLgpd', message: 'leads.confirmDelete', params: { name: this.d()!.lead.name }, danger: true, confirm: 'common.delete' })
      .subscribe(() => this.api.delete(`/leads/${this.data.id}`).subscribe(() => { this.toast.success('common.deleted'); this.ref.close(true); }));
  }
}
