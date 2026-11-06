import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService, isoDate, parseIsoDate } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { CampaignLookup, Lookup, MetricRow } from '../../../shared/models/models';

@Component({
  imports: [UI],
  templateUrl: './metric-form-dialog.html',
  styleUrl: './metric-form-dialog.css'
})
export class MetricFormDialog implements OnInit {
  readonly data = inject<{ row?: MetricRow; campaignId?: string | null }>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  readonly today = new Date();
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly ads = signal<Lookup[]>([]);
  private readonly r = this.data.row;
  readonly form = inject(FormBuilder).nonNullable.group({
    campaignId: [this.r?.campaignId ?? this.data.campaignId ?? '', Validators.required],
    advertisementId: [this.r?.advertisementId ?? null as string | null],
    date: [this.r ? parseIsoDate(this.r.date) : new Date(Date.now() - 86400000), Validators.required],
    impressions: [this.r?.impressions ?? 0, [Validators.required, Validators.min(0)]],
    reach: [this.r?.reach ?? 0, [Validators.required, Validators.min(0)]],
    clicks: [this.r?.clicks ?? 0, [Validators.required, Validators.min(0)]],
    spend: [this.r?.spend ?? 0, [Validators.required, Validators.min(0)]],
    leads: [this.r?.leads ?? 0, [Validators.required, Validators.min(0)]],
    conversions: [this.r?.conversions ?? 0, [Validators.required, Validators.min(0)]],
    attributedRevenue: [this.r?.attributedRevenue ?? 0, [Validators.required, Validators.min(0)]]
  });
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  /** Prévia com as mesmas fórmulas do backend (MetricCalculator); o valor oficial é sempre calculado no servidor. */
  readonly calc = computed(() => {
    const v = this.value();
    const div = (a: number, b: number, k = 1) => b > 0 ? a / b * k : null;
    return { ctr: div(v.clicks ?? 0, v.impressions ?? 0, 100), cpc: div(v.spend ?? 0, v.clicks ?? 0), cpm: div(v.spend ?? 0, v.impressions ?? 0, 1000),
      cpl: div(v.spend ?? 0, v.leads ?? 0), cpa: div(v.spend ?? 0, v.conversions ?? 0), roas: div(v.attributedRevenue ?? 0, v.spend ?? 0) };
  });
  readonly inconsistent = computed(() => { const v = this.value(); return (v.clicks ?? 0) > (v.impressions ?? 0) || (v.reach ?? 0) > (v.impressions ?? 0); });

  constructor(private ref: MatDialogRef<MetricFormDialog>) {}

  ngOnInit(): void {
    this.lookups.campaignsFor(null).subscribe(c => this.campaigns.set(c));
    this.loadAds(false);
  }

  loadAds(reset: boolean): void {
    if (reset) this.form.patchValue({ advertisementId: null });
    const id = this.form.value.campaignId;
    if (id) this.api.get<Lookup[]>('/advertisements/lookup', { campaignId: id }).subscribe(a => this.ads.set(a));
  }

  save(): void {
    const v = this.form.getRawValue();
    const body = { ...v, date: isoDate(v.date) };
    const req = this.data.row ? this.api.put<MetricRow>(`/metrics/${this.data.row.id}`, body) : this.api.post<MetricRow>('/metrics', body);
    req.subscribe(m => { this.toast.success('common.saved'); this.ref.close(m); });
  }
}
