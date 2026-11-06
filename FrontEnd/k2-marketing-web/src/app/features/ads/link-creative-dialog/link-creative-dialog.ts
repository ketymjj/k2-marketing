import { Component, OnInit, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { SecureImageComponent } from '../../../shared/components/secure-image/secure-image';
import { Advertisement, Creative, Paged } from '../../../shared/models/models';

/** Vincula criativos (do mesmo cliente) ao anúncio. */
@Component({
  imports: [UI, SecureImageComponent],
  templateUrl: './link-creative-dialog.html',
  styleUrl: './link-creative-dialog.css'
})
export class LinkCreativeDialog implements OnInit {
  readonly data = inject<Advertisement>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  readonly ad = signal<Advertisement>(this.data);
  readonly creatives = signal<Creative[]>([]);
  readonly busy = signal(false);

  ngOnInit(): void {
    this.api.get<Paged<Creative>>('/creatives', { clientId: this.data.clientId, pageSize: 200 }).subscribe(r => this.creatives.set(r.items));
  }

  isLinked(id: string): boolean { return this.ad().creatives.some(c => c.id === id); }

  toggle(c: Creative): void {
    this.busy.set(true);
    const url = `/advertisements/${this.ad().id}/creatives/${c.id}`;
    const req = this.isLinked(c.id) ? this.api.delete<Advertisement>(url) : this.api.post<Advertisement>(url);
    req.subscribe({ next: a => { this.ad.set(a); this.busy.set(false); this.toast.success('common.saved'); }, error: () => this.busy.set(false) });
  }
}
