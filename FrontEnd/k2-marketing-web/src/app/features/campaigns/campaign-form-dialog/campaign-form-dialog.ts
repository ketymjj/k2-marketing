import { Component, Inject, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService, isoDate, parseIsoDate } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { Campaign, CampaignStatus, ClientLookup, OBJECTIVES, PLATFORMS } from '../../../shared/models/models';

@Component({
  imports: [UI],
  templateUrl: './campaign-form-dialog.html',
  styleUrl: './campaign-form-dialog.css'
})
export class CampaignFormDialog implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  readonly platforms = PLATFORMS;
  readonly objectives = OBJECTIVES;
  readonly initialStatuses: CampaignStatus[] = ['Draft', 'Scheduled', 'Active'];
  readonly clients = signal<ClientLookup[]>([]);
  readonly saving = signal(false);
  readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    clientId: ['', Validators.required],
    platform: ['MetaAds', Validators.required],
    objective: ['LeadGeneration', Validators.required],
    status: ['Draft' as CampaignStatus],
    startDate: [new Date() as Date | null, Validators.required],
    endDate: [null as Date | null],
    plannedBudget: [0, [Validators.required, Validators.min(0)]]
  });

  constructor(@Inject(MAT_DIALOG_DATA) public data: { id?: string; clientId?: string }, private ref: MatDialogRef<CampaignFormDialog>) {}

  ngOnInit(): void {
    this.lookups.clients().subscribe(c => this.clients.set(c));
    if (this.data.clientId) this.form.patchValue({ clientId: this.data.clientId });
    if (this.data.id) this.api.get<Campaign>(`/campaigns/${this.data.id}`).subscribe(c => this.form.patchValue({
      name: c.name, clientId: c.clientId, platform: c.platform, objective: c.objective, plannedBudget: c.plannedBudget,
      startDate: parseIsoDate(c.startDate), endDate: c.endDate ? parseIsoDate(c.endDate) : null
    }));
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);
    const v = this.form.getRawValue();
    const body = { ...v, startDate: isoDate(v.startDate), endDate: isoDate(v.endDate), status: this.data.id ? null : v.status };
    const req = this.data.id ? this.api.put<Campaign>(`/campaigns/${this.data.id}`, body) : this.api.post<Campaign>('/campaigns', body);
    req.subscribe({
      next: c => { this.toast.success('common.saved'); this.lookups.invalidate(); this.ref.close(c); },
      error: () => this.saving.set(false)
    });
  }
}
