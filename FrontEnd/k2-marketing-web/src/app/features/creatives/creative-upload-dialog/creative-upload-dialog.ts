import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { API_URL } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { CREATIVE_TYPES, ClientLookup, Creative, CreativeType } from '../../../shared/models/models';
import { ACCEPT, MAX_MB } from '../creatives.shared';

@Component({
  imports: [UI],
  templateUrl: './creative-upload-dialog.html',
  styleUrl: './creative-upload-dialog.css'
})
export class CreativeUploadDialog implements OnInit {
  readonly data = inject<{ clientId?: string | null }>(MAT_DIALOG_DATA);
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  readonly types = CREATIVE_TYPES;
  readonly clients = signal<ClientLookup[]>([]);
  readonly file = signal<File | null>(null);
  readonly fileError = signal<string | null>(null);
  readonly progress = signal<number | null>(null);
  readonly form = this.fb.group({
    clientId: ['', Validators.required], type: ['Image' as CreativeType], name: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''], textContent: ['']
  });

  constructor(private ref: MatDialogRef<CreativeUploadDialog>) {}

  ngOnInit(): void {
    this.lookups.clients().subscribe(c => this.clients.set(c));
    if (this.data.clientId) this.form.patchValue({ clientId: this.data.clientId });
  }

  accept(): string { return ACCEPT[this.form.value.type as CreativeType]; }
  maxMb(): number { return MAX_MB[this.form.value.type as CreativeType]; }

  valid(): boolean {
    if (this.form.invalid) return false;
    return this.form.value.type === 'Text' ? !!this.form.value.textContent?.trim() : !!this.file() && !this.fileError();
  }

  onFile(e: Event): void { this.pick((e.target as HTMLInputElement).files?.[0] ?? null); }
  onDrop(e: DragEvent): void { e.preventDefault(); this.pick(e.dataTransfer?.files?.[0] ?? null); }

  private pick(f: File | null): void {
    this.fileError.set(null);
    this.file.set(f);
    if (!f) return;
    const ext = '.' + (f.name.split('.').pop() ?? '').toLowerCase();
    if (!this.accept().split(',').includes(ext)) this.fileError.set('creatives.invalidType');
    else if (f.size > this.maxMb() * 1024 * 1024) this.fileError.set('creatives.tooLarge');
    if (!this.form.value.name) this.form.patchValue({ name: f.name.replace(/\.[^.]+$/, '') });
  }

  save(): void {
    const v = this.form.getRawValue();
    const fd = new FormData();
    fd.append('clientId', v.clientId!);
    fd.append('name', v.name!);
    fd.append('type', v.type!);
    if (v.description) fd.append('description', v.description);
    if (v.type === 'Text') fd.append('textContent', v.textContent ?? '');
    else if (this.file()) fd.append('file', this.file()!);
    this.progress.set(0);
    this.http.post<Creative>(`${API_URL}/creatives`, fd, { reportProgress: true, observe: 'events' }).subscribe({
      next: ev => {
        if (ev.type === 1 && ev.total) this.progress.set(Math.round(ev.loaded / ev.total * 100));
        if (ev.type === 4) { this.toast.success('creatives.uploaded'); this.ref.close(ev.body); }
      },
      error: () => this.progress.set(null)
    });
  }
}
