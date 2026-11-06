import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService, isoDate, parseIsoDate } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { Budget, CampaignLookup } from '../../../shared/models/models';

@Component({
  imports: [UI],
  templateUrl: './budget-form-dialog.html',
  styleUrl: './budget-form-dialog.css'
})
export class BudgetFormDialog implements OnInit {
  readonly data = inject<{ budget?: Budget; campaignId?: string }>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly form = inject(FormBuilder).group({
    campaignId: [this.data.budget?.campaignId ?? this.data.campaignId ?? '', Validators.required],
    plannedAmount: [this.data.budget?.plannedAmount ?? null as number | null, [Validators.required, Validators.min(1)]],
    startDate: [this.data.budget ? parseIsoDate(this.data.budget.startDate) : new Date() as Date | null, Validators.required],
    endDate: [this.data.budget ? parseIsoDate(this.data.budget.endDate) : null as Date | null, Validators.required],
    notes: [this.data.budget?.notes ?? '']
  });

  constructor(private ref: MatDialogRef<BudgetFormDialog>) {}

  ngOnInit(): void {
    this.lookups.campaignsFor(null).subscribe(c => this.campaigns.set(c));
    if (this.data.budget) this.form.controls.campaignId.disable();
  }

  save(): void {
    const v = this.form.getRawValue();
    const body = { ...v, startDate: isoDate(v.startDate), endDate: isoDate(v.endDate) };
    const req = this.data.budget ? this.api.put<Budget>(`/budgets/${this.data.budget.id}`, body) : this.api.post<Budget>('/budgets', body);
    req.subscribe(b => { this.toast.success('common.saved'); this.ref.close(b); });
  }
}
