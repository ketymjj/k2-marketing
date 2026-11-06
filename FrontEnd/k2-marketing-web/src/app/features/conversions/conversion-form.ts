import { MatDialog } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';
import { Conversion } from '../../shared/models/models';
import { ConversionFormDialog } from './conversion-form-dialog/conversion-form-dialog';

export function openConversionForm(dialog: MatDialog, data: { campaignId?: string; clientId?: string; leadId?: string }): Observable<Conversion | undefined> {
  return dialog.open(ConversionFormDialog, { data, width: '680px', maxWidth: '95vw' }).afterClosed().pipe(map(r => r as Conversion | undefined));
}
