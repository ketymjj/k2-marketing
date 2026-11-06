import { Component, booleanAttribute, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoModule } from '@jsverse/transloco';
import { LearningService } from '../../../core/services/learning.service';

/**
 * Dica contextual exibida somente no Modo Iniciante.
 * `text` é uma chave de tradução; `items` lista chaves extras (ex.: explicação de cada campo do formulário).
 */
@Component({
  selector: 'k2-tip',
  imports: [MatIconModule, TranslocoModule],
  templateUrl: './tip.html',
  styleUrl: './tip.css'
})
export class TipComponent {
  readonly learning = inject(LearningService);
  readonly text = input.required<string>();
  readonly items = input<{ label: string; text: string }[]>([]);
  readonly compact = input(false, { transform: booleanAttribute });
}
