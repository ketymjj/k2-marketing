import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoModule } from '@jsverse/transloco';

@Component({
  selector: 'k2-page-header',
  imports: [MatIconModule, TranslocoModule],
  templateUrl: './page-header.html',
  styleUrl: './page-header.css'
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  readonly icon = input<string>();
  /** Título é dado do usuário (ex.: nome da campanha) e não deve ser traduzido. */
  readonly raw = input(false);
  readonly rawSubtitle = input(false);
}
