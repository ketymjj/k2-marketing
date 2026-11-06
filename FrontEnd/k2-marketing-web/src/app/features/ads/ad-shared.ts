import { MatDialog } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Advertisement } from '../../shared/models/models';
import { AdFormDialog } from './ad-form-dialog/ad-form-dialog';

export function openAdForm(dialog: MatDialog, data: { id?: string; campaignId?: string }): Observable<Advertisement | undefined> {
  return dialog.open(AdFormDialog, { data, width: '1000px', maxWidth: '96vw' }).afterClosed().pipe(map(r => r as Advertisement | undefined));
}
export function canEditAds(auth: AuthService): boolean { return auth.canOperate(); }
