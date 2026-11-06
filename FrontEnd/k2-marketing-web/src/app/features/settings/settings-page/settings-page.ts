import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { UI } from '../../../shared/ui';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { AgencyService } from '../../../core/services/app-state.services';
import { AppLang, LanguageService } from '../../../core/i18n/i18n';
import { ThemeService } from '../../../core/services/theme.service';
import { AgencySettings, AuditLog, CurrentUser, Integration, Paged } from '../../../shared/models/models';
import { PagedList, debounce } from '../../../shared/paged-list';

@Component({
  imports: [UI],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.css'
})
export class SettingsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly agency = inject(AgencyService);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly lang = inject(LanguageService);
  readonly theme = inject(ThemeService);
  readonly integrations = signal<Integration[]>([]);
  readonly entities = ['Client', 'Campaign', 'Advertisement', 'CreativeAsset', 'CampaignBudget', 'Lead', 'Conversion', 'CampaignMetric', 'MarketingTask', 'User', 'AgencySettings'];
  auditSearch = '';
  auditEntity: string | null = null;
  readonly audit = new PagedList<AuditLog>(p => this.api.get<Paged<AuditLog>>('/settings/audit', p), () => ({ search: this.auditSearch, entity: this.auditEntity }));
  readonly auditChanged = debounce(() => this.audit.load(true));

  readonly pwd = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,100}$/)]]
  });
  readonly agencyForm = this.fb.group({
    agencyName: ['', Validators.required], primaryColor: ['#6941C6', [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)]],
    contactEmail: ['', Validators.email], website: [''], reportFooter: [''], currency: ['BRL']
  });

  ngOnInit(): void {
    this.api.get<AgencySettings>('/settings/agency').subscribe(s => this.agencyForm.patchValue(s));
    if (!this.auth.isAdmin()) this.agencyForm.disable();
  }

  onTab(i: number): void {
    if (i === 2 && !this.integrations().length) this.api.get<Integration[]>('/settings/integrations').subscribe(r => this.integrations.set(r));
    if (i === 3 && !this.audit.loaded()) this.audit.load();
  }

  setLang(l: AppLang): void {
    this.lang.set(l);
    this.api.put<CurrentUser>('/auth/me/preferences', { language: l }).subscribe(u => this.auth.updateUser(u));
  }

  changePassword(): void {
    this.api.post('/auth/change-password', this.pwd.getRawValue()).subscribe(() => { this.toast.success('settings.passwordChanged'); this.pwd.reset(); });
  }

  saveAgency(): void {
    this.api.put<AgencySettings>('/settings/agency', this.agencyForm.getRawValue()).subscribe(s => {
      this.toast.success('common.saved');
      this.agency.settings.set(s);
    });
  }
}
