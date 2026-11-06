import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { NotificationCenter } from '../../../core/services/app-state.services';
import { AppNotification, Paged } from '../../../shared/models/models';
import { PagedList } from '../../../shared/paged-list';
import { NotificationTextPipe } from '../notification-text.pipe';

@Component({
  imports: [UI, NotificationTextPipe],
  templateUrl: './notifications-page.html',
  styleUrl: './notifications-page.css'
})
export class NotificationsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly center = inject(NotificationCenter);
  private readonly router = inject(Router);
  unreadOnly = false;
  readonly list = new PagedList<AppNotification>(p => this.api.get<Paged<AppNotification>>('/notifications', p), () => ({ unreadOnly: this.unreadOnly || null }));

  ngOnInit(): void { this.list.load(); }

  icon(type: string): string {
    return ({ CampaignEnding: 'event_busy', BudgetThreshold: 'savings', TaskOverdue: 'alarm', NewLead: 'person_add', ReportAvailable: 'description' } as Record<string, string>)[type] ?? 'notifications';
  }

  open(n: AppNotification): void {
    this.center.markRead(n);
    if (n.link) this.router.navigateByUrl(n.link);
    else this.list.load();
  }

  markAll(): void { this.center.markAllRead().subscribe(() => this.list.load()); }
}
