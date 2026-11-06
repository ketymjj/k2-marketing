import { Component, Inject, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { Client } from '../../../shared/models/models';

@Component({
  imports: [UI],
  templateUrl: './client-form-dialog.html',
  styleUrl: './client-form-dialog.css'
})
export class ClientFormDialog implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  readonly saving = signal(false);
  readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    companyName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    phone: [''], document: [''], industry: [''],
    website: ['', Validators.pattern(/^https?:\/\/.+/i)],
    monthlyBudget: [0, [Validators.required, Validators.min(0)]]
  });

  constructor(@Inject(MAT_DIALOG_DATA) public data: { id?: string }, private ref: MatDialogRef<ClientFormDialog>) {}

  ngOnInit(): void {
    if (this.data.id) this.api.get<Client>(`/clients/${this.data.id}`).subscribe(c => this.form.patchValue({ ...c, phone: c.phone ?? '', document: c.document ?? '', industry: c.industry ?? '', website: c.website ?? '' }));
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);
    const body = this.form.getRawValue();
    const req = this.data.id ? this.api.put<Client>(`/clients/${this.data.id}`, body) : this.api.post<Client>('/clients', body);
    req.subscribe({
      next: c => { this.toast.success('common.saved'); this.ref.close(c); },
      error: () => this.saving.set(false)
    });
  }
}
