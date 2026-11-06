import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { AD_STATUSES, Advertisement, CampaignLookup, LinkedCreative } from '../../../shared/models/models';
import { AdPreviewComponent } from '../ad-preview/ad-preview';

@Component({
  imports: [UI, AdPreviewComponent],
  templateUrl: './ad-form-dialog.html',
  styleUrl: './ad-form-dialog.css'
})
export class AdFormDialog implements OnInit {
  readonly data = inject<{ id?: string; campaignId?: string }>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  readonly statuses = AD_STATUSES;
  readonly ctas = ['Saiba mais', 'Comprar agora', 'Cadastre-se', 'Agendar', 'Fale conosco', 'Baixar', 'Learn more', 'Shop now', 'Sign up'];
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly creatives = signal<LinkedCreative[]>([]);
  readonly clientName = signal('');
  readonly saving = signal(false);
  readonly form = this.fb.group({
    campaignId: ['', Validators.required], name: ['', Validators.required], headline: ['', Validators.required], primaryText: ['', Validators.required],
    description: [''], callToAction: ['Saiba mais', Validators.required], status: ['Draft'],
    destinationUrl: ['https://', [Validators.required, Validators.pattern(/^https?:\/\/.+\..+/i)]]
  });

  constructor(private ref: MatDialogRef<AdFormDialog>) {}

  ngOnInit(): void {
    this.lookups.campaignsFor(null).subscribe(c => this.campaigns.set(c));
    this.lookups.clients().subscribe(clients => {
      const update = () => {
        const camp = this.campaigns().find(c => c.id === this.form.value.campaignId);
        this.clientName.set(clients.find(c => c.id === camp?.clientId)?.name ?? '');
      };
      this.form.controls.campaignId.valueChanges.subscribe(update);
      update();
    });
    if (this.data.campaignId) this.form.patchValue({ campaignId: this.data.campaignId });
    if (this.data.id) this.api.get<Advertisement>(`/advertisements/${this.data.id}`).subscribe(a => {
      this.form.patchValue({ ...a, description: a.description ?? '' });
      this.creatives.set(a.creatives);
    });
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);
    const body = this.form.getRawValue();
    const req = this.data.id ? this.api.put<Advertisement>(`/advertisements/${this.data.id}`, body) : this.api.post<Advertisement>('/advertisements', body);
    req.subscribe({ next: a => { this.toast.success('common.saved'); this.ref.close(a); }, error: () => this.saving.set(false) });
  }
}
