import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { AppNotification } from '../../shared/models/models';
import { LanguageService } from '../../core/i18n/i18n';

/** Mensagem traduzida da notificação a partir do tipo + parâmetros (nomes de campanhas/leads não são traduzidos). */
@Pipe({ name: 'notificationText', pure: false })
export class NotificationTextPipe implements PipeTransform {
  private readonly t = inject(TranslocoService);
  private readonly lang = inject(LanguageService);

  transform(n: AppNotification): string {
    this.lang.lang();
    const key = `notifications.types.${n.type}`;
    const params: Record<string, unknown> = { ...n.data };
    if (typeof params['month'] === 'string') {
      const [y, m] = (params['month'] as string).split('-').map(Number);
      params['month'] = new Intl.DateTimeFormat(this.lang.lang(), { month: 'long', year: 'numeric' }).format(new Date(y, m - 1, 1));
    }
    const text = this.t.translate(key, params);
    return text === key ? n.title : text;
  }
}
