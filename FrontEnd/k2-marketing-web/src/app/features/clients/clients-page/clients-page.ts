import { Component, OnInit, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { ClientListItem, ClientStatus, Paged } from '../../../shared/models/models';
import { PagedList, confirm, debounce } from '../../../shared/paged-list';
import { ClientFormDialog } from '../client-form-dialog/client-form-dialog';

@Component({
  imports: [UI],
  templateUrl: './clients-page.html',
  styleUrl: './clients-page.css'
})
export class ClientsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  readonly auth = inject(AuthService);
  readonly router = inject(Router);
  readonly industries = signal<string[]>([]);
  readonly columns = ['name', 'industry', 'contact', 'campaigns', 'monthlyBudget', 'status', 'actions'];
  search = '';
  status: ClientStatus | null = null;
  industry: string | null = null;
  readonly list = new PagedList<ClientListItem>(p => this.api.get<Paged<ClientListItem>>('/clients', p),
    () => ({ search: this.search, status: this.status, industry: this.industry }));
  readonly searchChanged = debounce(() => this.list.load(true));

  ngOnInit(): void {
    this.list.load();
    this.api.get<string[]>('/clients/industries').subscribe(i => this.industries.set(i));
  }

  openForm(id?: string): void {
    this.dialog.open(ClientFormDialog, { data: { id }, width: '720px', maxWidth: '95vw' }).afterClosed().subscribe(saved => {
      if (!saved) return;
      this.lookups.invalidate();
      this.list.load();
    });
  }

  toggleStatus(c: ClientListItem): void {
    const next: ClientStatus = c.status === 'Active' ? 'Inactive' : 'Active';
    confirm(this.dialog, {
      title: next === 'Inactive' ? 'clients.deactivate' : 'clients.activate',
      message: next === 'Inactive' ? 'clients.confirmDeactivate' : 'clients.confirmActivate', params: { name: c.name }, danger: next === 'Inactive'
    }).subscribe(() => this.api.patch(`/clients/${c.id}/status`, { status: next }).subscribe(() => {
      this.toast.success('common.saved');
      this.lookups.invalidate();
      this.list.load();
    }));
  }
}
