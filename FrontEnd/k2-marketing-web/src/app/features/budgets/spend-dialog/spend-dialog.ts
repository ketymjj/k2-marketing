import { Component, inject } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService, isoDate, parseIsoDate } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { Budget } from '../../../shared/models/models';

/** Registro de gasto realizado: soma ao investimento do dia (métrica de nível campanha). */
@Component({
  imports: [UI],
  templateUrl: './spend-dialog.html',
  styleUrl: './spend-dialog.css'
})
export class SpendDialog {
  readonly data = inject<Budget>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  readonly min = parseIsoDate(this.data.startDate);
  readonly max = new Date(Math.min(parseIsoDate(this.data.endDate).getTime(), Date.now()));
  readonly form = inject(FormBuilder).group({ date: [this.max as Date | null, Validators.required], amount: [null as number | null, [Validators.required, Validators.min(0.01)]] });
  constructor(private ref: MatDialogRef<SpendDialog>) {}
  save(): void {
    const v = this.form.getRawValue();
    this.api.post<Budget>(`/budgets/${this.data.id}/spend`, { date: isoDate(v.date), amount: v.amount }).subscribe(b => { this.toast.success('budgets.spendRegistered'); this.ref.close(b); });
  }
}
