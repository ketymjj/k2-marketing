import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { map } from 'rxjs';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { CONVERSION_TYPES, CampaignLookup, Conversion, ConversionType, Lead, Paged } from '../../../shared/models/models';

@Component({
  imports: [UI],
  templateUrl: './conversion-form-dialog.html',
  styleUrl: './conversion-form-dialog.css'
})
export class ConversionFormDialog implements OnInit {
  readonly data = inject<{ campaignId?: string; clientId?: string; leadId?: string }>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  readonly types = CONVERSION_TYPES;
  readonly today = new Date();
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly leads = signal<Lead[]>([]);
  readonly form = inject(FormBuilder).group({
    campaignId: [this.data.campaignId ?? '', Validators.required],
    leadId: [this.data.leadId ?? null as string | null],
    conversionType: [(this.data.leadId ? 'Purchase' : 'Purchase') as ConversionType],
    date: [new Date() as Date | null, Validators.required],
    time: [new Date().toTimeString().slice(0, 5)],
    revenueAmount: [0 as number | null, Validators.min(0)],
    externalId: [''], notes: ['']
  });

  constructor(private ref: MatDialogRef<ConversionFormDialog>) {}

  ngOnInit(): void {
    this.lookups.campaignsFor(this.data.clientId ?? null).subscribe(c => { this.campaigns.set(c); this.loadLeads(false); });
  }

  loadLeads(reset = true): void {
    if (reset) this.form.patchValue({ leadId: null });
    const camp = this.campaigns().find(c => c.id === this.form.value.campaignId);
    if (camp) this.api.get<Paged<Lead>>('/leads', { clientId: camp.clientId, pageSize: 500, sortBy: 'name' }).subscribe(r => this.leads.set(r.items));
  }

  save(): void {
    const v = this.form.getRawValue();
    const [h, m] = (v.time || '12:00').split(':').map(Number);
    const d = new Date(v.date!);
    d.setHours(h, m, 0, 0);
    this.api.post<Conversion>('/conversions', {
      campaignId: v.campaignId, leadId: v.leadId, conversionType: v.conversionType, conversionDate: d.toISOString(),
      revenueAmount: v.revenueAmount ?? 0, externalId: v.externalId || null, notes: v.notes || null
    }).subscribe(c => { this.toast.success('conversions.registered'); this.ref.close(c); });
  }
}
