import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { TranslocoModule } from '@jsverse/transloco';
import { addDays, isoDate } from '../../../core/services/api.service';
import { LookupService } from '../../../core/services/app-state.services';
import { AdPlatform, CampaignLookup, ClientLookup, PLATFORMS } from '../../models/models';
import { AuthService } from '../../../core/auth/auth.service';
import { AnalyticsFilter, Preset } from '../analytics.shared';

/** Filtros globais: cliente, plataforma, campanha e período (com atalhos). */
@Component({
  selector: 'k2-filter-bar',
  imports: [CommonModule, FormsModule, MatFormFieldModule, MatSelectModule, MatDatepickerModule, MatInputModule, MatIconModule, MatButtonModule, TranslocoModule],
  templateUrl: './filter-bar.html',
  styleUrl: './filter-bar.css'
})
export class FilterBarComponent implements OnInit {
  readonly showClient = input(true);
  readonly showPlatform = input(true);
  readonly showCampaign = input(true);
  readonly defaultPreset = input<Preset>('30d');
  readonly initial = input<Partial<AnalyticsFilter> | null>(null);
  readonly changed = output<AnalyticsFilter>();

  readonly auth = inject(AuthService);
  private readonly lookups = inject(LookupService);
  readonly platforms = PLATFORMS;
  readonly presets: Preset[] = ['7d', '30d', '90d', 'month', 'lastMonth', 'custom'];
  readonly clients = signal<ClientLookup[]>([]);
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly filteredCampaigns = computed(() => this.campaigns().filter(c => !this.platformSig() || c.platform === this.platformSig()));
  private readonly platformSig = signal<AdPlatform | null>(null);

  clientId: string | null = null;
  campaignId: string | null = null;
  preset: Preset = '30d';
  start: Date | null = null;
  end: Date | null = null;
  get platform(): AdPlatform | null { return this.platformSig(); }
  set platform(v: AdPlatform | null) { this.platformSig.set(v); }

  ngOnInit(): void {
    this.preset = this.defaultPreset();
    const init = this.initial();
    if (init) {
      this.clientId = init.clientId ?? null;
      this.campaignId = init.campaignId ?? null;
      this.platform = init.platform ?? null;
      if (init.from && init.to) {
        this.preset = 'custom';
        this.start = new Date(init.from + 'T00:00');
        this.end = new Date(init.to + 'T00:00');
      }
    }
    if (this.preset !== 'custom') this.applyPreset();
    if (!this.auth.isClientViewer()) this.lookups.clients().subscribe(c => this.clients.set(c));
    this.loadCampaigns();
    this.emit();
  }

  onClient(): void {
    this.campaignId = null;
    this.loadCampaigns();
    this.emit();
  }

  onPreset(): void {
    if (this.preset !== 'custom') {
      this.applyPreset();
      this.emit();
    }
  }

  emit(): void {
    if (this.campaignId && !this.filteredCampaigns().some(c => c.id === this.campaignId) && this.campaigns().length) this.campaignId = null;
    this.changed.emit({ clientId: this.clientId, platform: this.platform, campaignId: this.campaignId, from: isoDate(this.start), to: isoDate(this.end) });
  }

  private loadCampaigns(): void {
    if (this.showCampaign()) this.lookups.campaignsFor(this.clientId).subscribe(c => this.campaigns.set(c));
  }

  private applyPreset(): void {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    switch (this.preset) {
      case '7d': this.start = addDays(today, -6); this.end = today; break;
      case '30d': this.start = addDays(today, -29); this.end = today; break;
      case '90d': this.start = addDays(today, -89); this.end = today; break;
      case 'month': this.start = new Date(today.getFullYear(), today.getMonth(), 1); this.end = today; break;
      case 'lastMonth':
        this.start = new Date(today.getFullYear(), today.getMonth() - 1, 1);
        this.end = new Date(today.getFullYear(), today.getMonth(), 0);
        break;
    }
  }
}
