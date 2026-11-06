import { ApplicationConfig, inject, isDevMode, provideAppInitializer, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { DateAdapter, provideNativeDateAdapter } from '@angular/material/core';
import { LocaleDateAdapter } from './core/i18n/date-adapter';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatIconRegistry } from '@angular/material/icon';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { routes } from './app.routes';
import { authInterceptor, errorInterceptor } from './core/interceptors/interceptors';
import { LANGS, LanguageService, TranslocoHttpLoader, storedLang } from './core/i18n/i18n';
import { ThemeService } from './core/services/theme.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding(), withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    provideAnimationsAsync(),
    provideNativeDateAdapter(),
    { provide: DateAdapter, useClass: LocaleDateAdapter },
    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'outline', subscriptSizing: 'dynamic' } },
    provideTransloco({
      config: {
        availableLangs: [...LANGS], defaultLang: 'pt-BR', fallbackLang: 'pt-BR', reRenderOnLangChange: true, prodMode: !isDevMode(),
        missingHandler: { logMissingKey: isDevMode() }
      },
      loader: TranslocoHttpLoader
    }),
    // Carrega o idioma salvo antes da primeira renderização (padrão pt-BR) e aplica o tema.
    provideAppInitializer(() => {
      inject(MatIconRegistry).setDefaultFontSetClass('material-icons-round');
      inject(ThemeService).init();
      inject(LanguageService).init();
      return firstValueFrom(inject(TranslocoService).load(storedLang()));
    })
  ]
};
