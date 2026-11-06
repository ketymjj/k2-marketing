import { Component, inject } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { Creative } from '../../../shared/models/models';

@Component({
  imports: [UI],
  templateUrl: './creative-edit-dialog.html',
  styleUrl: './creative-edit-dialog.css'
})
export class CreativeEditDialog {
  readonly data = inject<Creative>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  readonly form = inject(FormBuilder).group({ name: [this.data.name, Validators.required], description: [this.data.description ?? ''], textContent: [this.data.textContent ?? ''] });
  constructor(private ref: MatDialogRef<CreativeEditDialog>) {}
  save(): void {
    this.api.put<Creative>(`/creatives/${this.data.id}`, this.form.getRawValue()).subscribe(c => { this.toast.success('common.saved'); this.ref.close(c); });
  }
}
