import { Component, computed, input } from '@angular/core';

@Component({
  selector: 'k2-skeleton',
  templateUrl: './skeleton.html',
  styleUrl: './skeleton.css'
})
export class SkeletonComponent {
  readonly rows = input(5);
  readonly height = input(18);
  readonly rowsArr = computed(() => Array.from({ length: this.rows() }));
}
