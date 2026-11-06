import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';

/** Explicação de um indicador: o que significa, como calcular, exemplo e como interpretar. */
@Component({
  imports: [MatDialogModule, MatButtonModule, MatIconModule, RouterLink, TranslocoModule],
  templateUrl: './metric-help-dialog.html',
  styleUrl: './metric-help-dialog.css'
})
export class MetricHelpDialog {
  readonly metric = inject<string>(MAT_DIALOG_DATA);
}

export function openMetricHelp(dialog: MatDialog, metric: string): void {
  dialog.open(MetricHelpDialog, { data: metric, width: '560px', maxWidth: '95vw', autoFocus: false });
}
