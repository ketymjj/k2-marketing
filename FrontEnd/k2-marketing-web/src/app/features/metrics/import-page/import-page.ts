import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { UI } from '../../../shared/ui';
import { API_URL, ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { ImportPreview, ImportResult, ImportRowStatus } from '../../../shared/models/models';
import { Kind } from '../import.shared';

@Component({
  imports: [UI],
  templateUrl: './import-page.html',
  styleUrl: './import-page.css'
})
export class ImportPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  readonly statuses: ImportRowStatus[] = ['New', 'Duplicate', 'DuplicateInFile', 'Invalid'];
  readonly step = signal(1);
  readonly file = signal<File | null>(null);
  readonly p = signal<ImportPreview | null>(null);
  readonly result = signal<ImportResult | null>(null);
  readonly busy = signal(false);
  readonly statusSig = signal<ImportRowStatus | null>(null);
  readonly modeSig = signal<'Skip' | 'Overwrite'>('Skip');
  kind: Kind = 'metrics';
  get statusFilter() { return this.statusSig(); }
  set statusFilter(v) { this.statusSig.set(v); }
  get mode() { return this.modeSig(); }
  set mode(v) { this.modeSig.set(v); }

  readonly filteredRows = computed(() => (this.p()?.rows ?? []).filter(r => !this.statusSig() || r.status === this.statusSig()));
  valueCols(): string[] {
    return this.kind === 'metrics'
      ? ['impressions', 'clicks', 'spend', 'leads', 'conversions', 'attributed_revenue']
      : ['conversion_type', 'revenue_amount', 'external_id', 'lead_email'];
  }
  readonly importable = computed(() => {
    const p = this.p();
    if (!p) return 0;
    return p.newRows + (this.kind === 'metrics' && this.modeSig() === 'Overwrite' ? p.duplicates : 0);
  });

  ngOnInit(): void {
    if (this.route.snapshot.queryParamMap.get('kind') === 'conversions') this.kind = 'conversions';
  }

  columnsFor(): { name: string; required: boolean }[] {
    return this.kind === 'metrics'
      ? [{ name: 'date', required: true }, { name: 'campaign_id', required: false }, { name: 'campaign_name', required: false }, { name: 'ad_name', required: false },
        { name: 'impressions', required: true }, { name: 'reach', required: false }, { name: 'clicks', required: true }, { name: 'spend', required: true },
        { name: 'leads', required: false }, { name: 'conversions', required: false }, { name: 'attributed_revenue', required: false }]
      : [{ name: 'campaign_id', required: false }, { name: 'campaign_name', required: false }, { name: 'conversion_type', required: true },
        { name: 'conversion_date', required: true }, { name: 'revenue_amount', required: false }, { name: 'external_id', required: false }, { name: 'lead_email', required: false }];
  }

  reset(): void {
    this.step.set(1);
    this.p.set(null);
    this.result.set(null);
    this.file.set(null);
    this.statusSig.set(null);
    this.modeSig.set('Skip');
  }

  template(): void {
    this.api.download(`/imports/${this.kind}/template`, undefined, `k2-modelo-${this.kind}.csv`).subscribe();
  }

  onFile(e: Event): void { this.pick((e.target as HTMLInputElement).files?.[0] ?? null); }
  onDrop(e: DragEvent): void { e.preventDefault(); this.pick(e.dataTransfer?.files?.[0] ?? null); }

  private pick(f: File | null): void {
    if (f && f.size > 5 * 1024 * 1024) { this.toast.error('imports.tooLarge'); return; }
    if (f && !/\.(csv|txt)$/i.test(f.name)) { this.toast.error('imports.invalidType'); return; }
    this.file.set(f);
  }

  preview(): void {
    const fd = new FormData();
    fd.append('file', this.file()!);
    this.busy.set(true);
    this.http.post<ImportPreview>(`${API_URL}/imports/${this.kind}/preview`, fd).subscribe({
      next: p => { this.p.set(p); this.step.set(2); this.busy.set(false); },
      error: () => this.busy.set(false)
    });
  }

  confirmImport(): void {
    const fd = new FormData();
    fd.append('file', this.file()!);
    if (this.kind === 'metrics') fd.append('mode', this.modeSig());
    this.busy.set(true);
    this.http.post<ImportResult>(`${API_URL}/imports/${this.kind}/confirm`, fd).subscribe({
      next: r => { this.result.set(r); this.step.set(3); this.busy.set(false); this.toast.success('imports.done'); },
      error: () => this.busy.set(false)
    });
  }
}
