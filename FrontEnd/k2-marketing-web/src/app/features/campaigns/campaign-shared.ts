import { MatDialog } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { Campaign, CampaignStatus } from '../../shared/models/models';
import { confirm } from '../../shared/paged-list';
import { CampaignFormDialog } from './campaign-form-dialog/campaign-form-dialog';

/** Mesmas transições permitidas no domínio (Campaign.ChangeStatus). */
export const TRANSITIONS: Record<CampaignStatus, CampaignStatus[]> = {
  Draft: ['Scheduled', 'Active', 'Cancelled'],
  Scheduled: ['Draft', 'Active', 'Cancelled'],
  Active: ['Paused', 'Completed', 'Cancelled'],
  Paused: ['Active', 'Completed', 'Cancelled'],
  Completed: [],
  Cancelled: []
};
export const STATUS_ACTION: Record<CampaignStatus, { key: string; icon: string }> = {
  Draft: { key: 'campaigns.actions.toDraft', icon: 'edit_note' },
  Scheduled: { key: 'campaigns.actions.schedule', icon: 'schedule' },
  Active: { key: 'campaigns.actions.activate', icon: 'play_arrow' },
  Paused: { key: 'campaigns.actions.pause', icon: 'pause' },
  Completed: { key: 'campaigns.actions.complete', icon: 'flag' },
  Cancelled: { key: 'campaigns.actions.cancel', icon: 'cancel' }
};
/** Confirma e aplica a mudança de status interna (não altera a campanha na plataforma de anúncios). */
export function changeCampaignStatus(dialog: MatDialog, api: ApiService, toast: ToastService, c: { id: string; name: string }, status: CampaignStatus): Observable<Campaign> {
  return new Observable<Campaign>(sub => {
    confirm(dialog, {
      title: STATUS_ACTION[status].key, message: 'campaigns.confirmStatus', params: { name: c.name },
      danger: status === 'Cancelled'
    }).subscribe(() => api.patch<Campaign>(`/campaigns/${c.id}/status`, { status }).subscribe({
      next: r => { toast.success('campaigns.statusChanged'); sub.next(r); sub.complete(); },
      error: e => sub.error(e)
    }));
  });
}
export function openCampaignForm(dialog: MatDialog, data: { id?: string; clientId?: string }): Observable<Campaign | undefined> {
  return dialog.open(CampaignFormDialog, { data, width: '720px', maxWidth: '95vw' }).afterClosed().pipe(map(r => r as Campaign | undefined));
}
