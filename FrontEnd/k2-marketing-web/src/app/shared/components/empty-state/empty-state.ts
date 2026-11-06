import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoModule } from '@jsverse/transloco';

@Component({
  selector: 'k2-empty',
  imports: [MatIconModule, TranslocoModule],
  templateUrl: './empty-state.html',
  styleUrl: './empty-state.css'
})
export class EmptyStateComponent {
  readonly icon = input('inbox');
  readonly title = input('common.empty');
  readonly message = input<string>();
}
