import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { ClientLookup, ROLES, User, UserRole } from '../../../shared/models/models';
import { PASSWORD } from '../users.shared';

@Component({
  imports: [UI],
  templateUrl: './user-form-dialog.html',
  styleUrl: './user-form-dialog.css'
})
export class UserFormDialog implements OnInit {
  readonly data = inject<{ user?: User }>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  readonly auth = inject(AuthService);
  readonly roles = ROLES;
  readonly clients = signal<ClientLookup[]>([]);
  private readonly u = this.data.user;
  readonly form = inject(FormBuilder).group({
    fullName: [this.u?.fullName ?? '', Validators.required], email: [this.u?.email ?? '', [Validators.required, Validators.email]],
    password: ['', this.u ? [] : [Validators.required, Validators.pattern(PASSWORD)]],
    role: [this.u?.role ?? 'Analyst' as UserRole], clientId: [this.u?.clientId ?? null as string | null], active: [this.u?.active ?? true]
  });

  constructor(private ref: MatDialogRef<UserFormDialog>) {}

  ngOnInit(): void { this.lookups.clients().subscribe(c => this.clients.set(c)); }

  save(): void {
    const v = this.form.getRawValue();
    const req = this.u
      ? this.api.put<User>(`/users/${this.u.id}`, { fullName: v.fullName, email: v.email, role: v.role, clientId: v.role === 'ClientViewer' ? v.clientId : null, active: v.active })
      : this.api.post<User>('/users', { fullName: v.fullName, email: v.email, password: v.password, role: v.role, clientId: v.role === 'ClientViewer' ? v.clientId : null });
    req.subscribe(r => { this.toast.success('common.saved'); this.ref.close(r); });
  }
}
