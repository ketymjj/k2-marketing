import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { Creative } from '../../../shared/models/models';

@Component({
  imports: [UI],
  templateUrl: './creative-preview-dialog.html',
  styleUrl: './creative-preview-dialog.css'
})
export class CreativePreviewDialog implements OnInit, OnDestroy {
  readonly data = inject<Creative>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  readonly url = signal<string | null>(null);

  ngOnInit(): void {
    if (this.data.hasFile) this.api.blob(`/creatives/${this.data.id}/file`).subscribe(b => this.url.set(URL.createObjectURL(b)));
  }
  ngOnDestroy(): void { if (this.url()) URL.revokeObjectURL(this.url()!); }
}
