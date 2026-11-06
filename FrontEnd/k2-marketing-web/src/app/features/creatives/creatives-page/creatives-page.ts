import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService, saveBlob } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { SecureImageComponent } from '../../../shared/components/secure-image/secure-image';
import { CREATIVE_TYPES, ClientLookup, Creative, CreativeType, Paged } from '../../../shared/models/models';
import { PagedList, confirm, debounce } from '../../../shared/paged-list';
import { CreativeUploadDialog } from '../creative-upload-dialog/creative-upload-dialog';
import { CreativeEditDialog } from '../creative-edit-dialog/creative-edit-dialog';
import { CreativePreviewDialog } from '../creative-preview-dialog/creative-preview-dialog';

@Component({
  imports: [UI, SecureImageComponent],
  templateUrl: './creatives-page.html',
  styleUrl: './creatives-page.css'
})
export class CreativesPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  readonly auth = inject(AuthService);
  readonly types = CREATIVE_TYPES;
  readonly clients = signal<ClientLookup[]>([]);
  search = '';
  clientId: string | null = null;
  type: CreativeType | null = null;
  readonly list = new PagedList<Creative>(p => this.api.get<Paged<Creative>>('/creatives', p), () => ({ search: this.search, clientId: this.clientId, type: this.type }));
  readonly searchChanged = debounce(() => this.list.load(true));

  ngOnInit(): void {
    this.list.pageSize = 24;
    this.clientId = this.route.snapshot.queryParamMap.get('clientId');
    if (!this.auth.isClientViewer()) this.lookups.clients().subscribe(c => this.clients.set(c));
    this.list.load();
  }

  icon(t: CreativeType): string {
    return ({ Image: 'image', Video: 'smart_display', Banner: 'view_day', Carousel: 'view_carousel', Text: 'notes' } as const)[t];
  }

  setType(t: CreativeType | null): void { this.type = t; this.list.load(true); }

  upload(): void {
    this.dialog.open(CreativeUploadDialog, { data: { clientId: this.clientId }, width: '640px', maxWidth: '95vw' }).afterClosed().subscribe(r => r && this.list.load(true));
  }

  edit(c: Creative): void {
    this.dialog.open(CreativeEditDialog, { data: c, width: '560px', maxWidth: '95vw' }).afterClosed().subscribe(r => r && this.list.load());
  }

  preview(c: Creative): void { this.dialog.open(CreativePreviewDialog, { data: c, maxWidth: '96vw' }); }

  download(c: Creative): void { this.api.blob(`/creatives/${c.id}/file`).subscribe(b => saveBlob(b, c.originalFileName ?? c.name)); }

  remove(c: Creative): void {
    confirm(this.dialog, { title: 'common.delete', message: c.adsCount ? 'creatives.confirmDeleteLinked' : 'creatives.confirmDelete', params: { name: c.name, n: c.adsCount }, danger: true, confirm: 'common.delete' })
      .subscribe(() => this.api.delete(`/creatives/${c.id}`).subscribe(() => { this.toast.success('common.deleted'); this.list.load(); }));
  }
}
