import { Component, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { Paged, ROLES, User, UserRole } from '../../../shared/models/models';
import { PagedList, debounce } from '../../../shared/paged-list';
import { UserFormDialog } from '../user-form-dialog/user-form-dialog';
import { ResetPasswordDialog } from '../reset-password-dialog/reset-password-dialog';

@Component({
  imports: [UI],
  templateUrl: './users-page.html',
  styleUrl: './users-page.css'
})
export class UsersPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  readonly roles = ROLES;
  readonly columns = ['fullName', 'role', 'active', 'lastLoginAt', 'actions'];
  search = '';
  role: UserRole | null = null;
  readonly list = new PagedList<User>(p => this.api.get<Paged<User>>('/users', p), () => ({ search: this.search, role: this.role }));
  readonly searchChanged = debounce(() => this.list.load(true));

  ngOnInit(): void { this.list.load(); }

  openForm(user?: User): void {
    this.dialog.open(UserFormDialog, { data: { user }, width: '620px', maxWidth: '95vw' }).afterClosed().subscribe(r => r && this.list.load());
  }

  reset(user: User): void {
    this.dialog.open(ResetPasswordDialog, { data: user, width: '460px', maxWidth: '95vw' });
  }
}
