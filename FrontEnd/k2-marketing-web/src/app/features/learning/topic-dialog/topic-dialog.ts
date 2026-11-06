import { Component, computed, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoModule } from '@jsverse/transloco';
import { TOPICS } from '../learning.shared';

/** Explicação simples de um tema, com navegação para o anterior/próximo. */
@Component({
  imports: [MatDialogModule, MatButtonModule, MatIconModule, TranslocoModule],
  templateUrl: './topic-dialog.html',
  styleUrl: './topic-dialog.css'
})
export class TopicDialog {
  private readonly data = inject<{ key: string; index: number }>(MAT_DIALOG_DATA);
  readonly index = signal(this.data.index);
  readonly topic = computed(() => TOPICS[this.index()]);
  readonly total = TOPICS.length;

  move(step: number): void {
    this.index.set(Math.min(this.total - 1, Math.max(0, this.index() + step)));
  }
}
