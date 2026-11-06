import { Component, inject } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { User } from '../../../shared/models/models';
import { PASSWORD } from '../users.shared';

@Component({
  imports: [UI],
  templateUrl: './reset-password-dialog.html',
  styleUrl: './reset-password-dialog.css'
})
export class ResetPasswordDialog {
  readonly data = inject<User>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  readonly password = inject(FormBuilder).nonNullable.control('', [Validators.required, Validators.pattern(PASSWORD)]);
  constructor(private ref: MatDialogRef<ResetPasswordDialog>) {}
  save(): void {
    this.api.post(`/users/${this.data.id}/reset-password`, { newPassword: this.password.value }).subscribe(() => { this.toast.success('users.passwordReset'); this.ref.close(true); });
  }
}
