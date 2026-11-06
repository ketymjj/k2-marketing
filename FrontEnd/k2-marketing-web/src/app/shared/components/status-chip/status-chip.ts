import { Component, computed, input } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';

/** Selo colorido de status, traduzido a partir de enums.{kind}.{value}. */
@Component({
  selector: 'k2-status',
  imports: [TranslocoModule],
  templateUrl: './status-chip.html',
  styleUrl: './status-chip.css'
})
export class StatusChipComponent {
  readonly kind = input.required<string>();
  readonly value = input.required<string | null | undefined>();
  readonly cls = computed(() => `chip chip-${this.kind()}-${(this.value() ?? '').toLowerCase()}`);
}
