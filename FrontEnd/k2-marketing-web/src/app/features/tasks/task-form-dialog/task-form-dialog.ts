import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { UI } from '../../../shared/ui';
import { ApiService, isoDate, parseIsoDate } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { LookupService } from '../../../core/services/app-state.services';
import { CampaignLookup, ClientLookup, MarketingTask, PRIORITIES, TASK_STATUSES, TaskPriority, TaskStatus, UserLookup } from '../../../shared/models/models';

@Component({
  imports: [UI],
  templateUrl: './task-form-dialog.html',
  styleUrl: './task-form-dialog.css'
})
export class TaskFormDialog implements OnInit {
  readonly data = inject<{ task?: MarketingTask; date?: Date }>(MAT_DIALOG_DATA);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly lookups = inject(LookupService);
  readonly priorities = PRIORITIES;
  readonly statuses = TASK_STATUSES;
  readonly clients = signal<ClientLookup[]>([]);
  readonly campaigns = signal<CampaignLookup[]>([]);
  readonly users = signal<UserLookup[]>([]);
  private readonly t = this.data.task;
  readonly form = inject(FormBuilder).group({
    title: [this.t?.title ?? '', Validators.required], description: [this.t?.description ?? ''],
    clientId: [this.t?.clientId ?? null as string | null], campaignId: [this.t?.campaignId ?? null as string | null],
    assignedToId: [this.t?.assignedToId ?? null as string | null],
    dueDate: [this.t ? parseIsoDate(this.t.dueDate) : (this.data.date ?? new Date()), Validators.required],
    priority: [this.t?.priority ?? 'Medium' as TaskPriority], status: [this.t?.status ?? 'Todo' as TaskStatus]
  });

  constructor(private ref: MatDialogRef<TaskFormDialog>) {}

  ngOnInit(): void {
    this.lookups.clients().subscribe(c => this.clients.set(c));
    this.lookups.assignableUsers().subscribe(u => this.users.set(u));
    this.loadCampaigns(false);
  }

  loadCampaigns(reset: boolean): void {
    if (reset) this.form.patchValue({ campaignId: null });
    this.lookups.campaignsFor(this.form.value.clientId).subscribe(c => this.campaigns.set(c));
  }

  save(): void {
    const v = this.form.getRawValue();
    const body = { ...v, dueDate: isoDate(v.dueDate) };
    const req = this.t ? this.api.put(`/tasks/${this.t.id}`, body) : this.api.post('/tasks', body);
    req.subscribe(r => { this.toast.success('common.saved'); this.ref.close(r); });
  }
}
