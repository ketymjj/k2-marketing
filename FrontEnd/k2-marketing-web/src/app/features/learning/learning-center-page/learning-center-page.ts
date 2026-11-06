import { Component, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { LearningService } from '../../../core/services/learning.service';
import { TourService } from '../../../core/services/tour.service';
import { openMetricHelp } from '../../../shared/components/metric-help-dialog/metric-help-dialog';
import { LearnNavComponent } from '../learn-nav/learn-nav';
import { TopicDialog } from '../topic-dialog/topic-dialog';
import { METRIC_KEYS, TOPICS } from '../learning.shared';

@Component({
  imports: [UI, LearnNavComponent],
  templateUrl: './learning-center-page.html',
  styleUrl: './learning-center-page.css'
})
export class LearningCenterPage {
  private readonly dialog = inject(MatDialog);
  readonly learning = inject(LearningService);
  readonly tour = inject(TourService);
  readonly topics = TOPICS;
  readonly metrics = METRIC_KEYS;

  openTopic(key: string, index: number): void {
    this.dialog.open(TopicDialog, { data: { key, index }, width: '680px', maxWidth: '95vw', autoFocus: false });
  }

  openMetric(key: string): void { openMetricHelp(this.dialog, key); }
}
