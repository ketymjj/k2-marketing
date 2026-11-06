import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoModule } from '@jsverse/transloco';

/** Ícone ⓘ com explicação do campo (sempre disponível, independente do Modo Iniciante). */
@Component({
  selector: 'k2-field-help',
  imports: [MatIconModule, MatTooltipModule, TranslocoModule],
  templateUrl: './field-help.html',
  styleUrl: './field-help.css'
})
export class FieldHelpComponent {
  readonly text = input.required<string>();
}
