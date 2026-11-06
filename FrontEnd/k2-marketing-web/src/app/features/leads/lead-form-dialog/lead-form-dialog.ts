import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { CampaignLookup, ClientLookup, Lead } from '../../../shared/models/models';

@Component({
  imports: [UI],
  templateUrl: './lead-form-dialog.html',
  styleUrl: './lead-form-dialog.css'
})
export class LeadFormDialog implements OnInit {
  readonly data = inject<{ lead?: Lead; clientId?: string | null }>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  readonly clients = signal<ClientLookup[]>([]);
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly sources = signal<string[]>([]);
  readonly form = inject(FormBuilder).group({
    name: [this.data.lead?.name ?? '', Validators.required],
    email: [this.data.lead?.email ?? '', Validators.email],
    phone: [this.data.lead?.phone ?? ''],
    clientId: [this.data.lead?.clientId ?? this.data.clientId ?? '', Validators.required],
    campaignId: [this.data.lead?.campaignId ?? null as string | null],
    source: [this.data.lead?.source ?? ''],
    consentGiven: [this.data.lead?.consentGiven ?? true]
  });

  constructor(private ref: MatDialogRef<LeadFormDialog>) {}

  ngOnInit(): void {
    this.lookups.clients().subscribe(c => {
      this.clients.set(c);
      if (c.length === 1 && !this.form.value.clientId) this.form.patchValue({ clientId: c[0].id });
      this.loadCampaigns(false);
    });
    this.api.get<string[]>('/leads/sources').subscribe(s => this.sources.set(s));
    if (this.data.lead) this.form.controls.clientId.disable();
  }

  loadCampaigns(reset = true): void {
    if (reset) this.form.patchValue({ campaignId: null });
    const clientId = this.form.getRawValue().clientId;
    if (clientId) this.lookups.campaignsFor(clientId).subscribe(c => this.campaigns.set(c));
  }

  save(): void {
    const body = this.form.getRawValue();
    const req = this.data.lead ? this.api.put<Lead>(`/leads/${this.data.lead.id}`, body) : this.api.post<Lead>('/leads', body);
    req.subscribe(l => { this.toast.success('common.saved'); this.ref.close(l); });
  }
}
