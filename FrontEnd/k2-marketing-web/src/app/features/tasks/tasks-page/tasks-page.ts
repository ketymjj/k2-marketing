import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService, addDays, isoDate } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { LanguageService } from '../../../core/i18n/i18n';
import { KpiComponent } from '../../../shared/components/kpi/kpi';
import { CampaignLookup, MarketingTask, Paged, TASK_STATUSES, TaskStats, TaskStatus, UserLookup } from '../../../shared/models/models';
import { PagedList, confirm, debounce } from '../../../shared/paged-list';
import { TaskFormDialog } from '../task-form-dialog/task-form-dialog';
import { Day } from '../tasks.shared';

@Component({
  imports: [UI, KpiComponent],
  templateUrl: './tasks-page.html',
  styleUrl: './tasks-page.css'
})
export class TasksPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  private readonly lang = inject(LanguageService);
  readonly statuses = TASK_STATUSES;
  readonly stats = signal<TaskStats | null>(null);
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly users = signal<UserLookup[]>([]);
  readonly month = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  readonly calTasks = signal<MarketingTask[]>([]);
  view: 'calendar' | 'list' = 'calendar';
  search = '';
  campaignId: string | null = null;
  assignee: string | null = null;
  status: TaskStatus | null = null;
  overdueOnly = false;
  private filters = () => ({
    search: this.search, campaignId: this.campaignId, status: this.status, overdueOnly: this.overdueOnly || null,
    mine: this.assignee === 'mine' || null, assignedToId: this.assignee && this.assignee !== 'mine' ? this.assignee : null
  });
  readonly list = new PagedList<MarketingTask>(p => this.api.get<Paged<MarketingTask>>('/tasks', p), this.filters);
  readonly searchChanged = debounce(() => this.reload());

  readonly monthLabel = computed(() => new Intl.DateTimeFormat(this.lang.lang(), { month: 'long', year: 'numeric' }).format(this.month()));
  readonly weekdays = computed(() => {
    const fmt = new Intl.DateTimeFormat(this.lang.lang(), { weekday: 'short' });
    return Array.from({ length: 7 }, (_, i) => fmt.format(new Date(2024, 0, 1 + i))); // 01/01/2024 = segunda-feira
  });
  private readonly gridStart = computed(() => {
    const m = this.month();
    const offset = (m.getDay() + 6) % 7;
    return addDays(m, -offset);
  });
  readonly days = computed<Day[]>(() => {
    const start = this.gridStart();
    const todayIso = isoDate(new Date());
    const byDay = new Map<string, MarketingTask[]>();
    for (const t of this.calTasks()) byDay.set(t.dueDate, [...(byDay.get(t.dueDate) ?? []), t]);
    return Array.from({ length: 42 }, (_, i) => {
      const date = addDays(start, i);
      const iso = isoDate(date)!;
      return { date, iso, inMonth: date.getMonth() === this.month().getMonth(), today: iso === todayIso, tasks: byDay.get(iso) ?? [] };
    });
  });

  ngOnInit(): void {
    this.lookups.campaignsFor(null).subscribe(c => this.campaigns.set(c));
    this.lookups.assignableUsers().subscribe(u => this.users.set(u));
    this.reload();
  }

  reload(): void {
    this.api.get<TaskStats>('/tasks/stats').subscribe(s => this.stats.set(s));
    if (this.view === 'list') { this.list.load(true); return; }
    const start = this.gridStart();
    this.api.get<MarketingTask[]>('/tasks/calendar', { ...this.filters(), from: isoDate(start), to: isoDate(addDays(start, 41)) }).subscribe(t => this.calTasks.set(t));
  }

  move(n: number): void {
    const m = this.month();
    this.month.set(new Date(m.getFullYear(), m.getMonth() + n, 1));
    this.reload();
  }

  goToday(): void {
    this.month.set(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
    this.reload();
  }

  toggleDone(t: MarketingTask): void {
    this.api.patch(`/tasks/${t.id}/status`, { status: t.status === 'Done' ? 'Todo' : 'Done' }).subscribe(() => { this.toast.success('tasks.updated'); this.reload(); });
  }

  openForm(task?: MarketingTask, date?: Date): void {
    this.dialog.open(TaskFormDialog, { data: { task, date }, width: '680px', maxWidth: '95vw' }).afterClosed().subscribe(r => r && this.reload());
  }

  remove(t: MarketingTask): void {
    confirm(this.dialog, { title: 'common.delete', message: 'tasks.confirmDelete', params: { name: t.title }, danger: true, confirm: 'common.delete' })
      .subscribe(() => this.api.delete(`/tasks/${t.id}`).subscribe(() => { this.toast.success('common.deleted'); this.reload(); }));
  }
}
