import { MatDialog } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';
import { Budget } from '../../shared/models/models';
import { BudgetFormDialog } from './budget-form-dialog/budget-form-dialog';

export function openBudgetForm(dialog: MatDialog, data: { budget?: Budget; campaignId?: string }): Observable<Budget | undefined> {
  return dialog.open(BudgetFormDialog, { data, width: '620px', maxWidth: '95vw' }).afterClosed().pipe(map(r => r as Budget | undefined));
}
