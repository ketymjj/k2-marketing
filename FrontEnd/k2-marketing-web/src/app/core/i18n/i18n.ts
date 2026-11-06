import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Translation, TranslocoLoader, TranslocoService } from '@jsverse/transloco';
import { DateAdapter } from '@angular/material/core';

export type AppLang = 'pt-BR' | 'en-US';
export const LANGS: AppLang[] = ['pt-BR', 'en-US'];
const KEY = 'k2m.lang';

@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);
  getTranslation(lang: string) {
    return this.http.get<Translation>(`/i18n/${lang}.json`);
  }
}

export function storedLang(): AppLang {
  try {
    const v = localStorage.getItem(KEY);
    return v === 'en-US' ? 'en-US' : 'pt-BR';
  } catch {
    return 'pt-BR';
  }
}

/**
 * Idioma ativo: traduções, formatação de números/datas e calendário do Material.
 * Somente a interface é traduzida — nomes de campanhas, textos de anúncios e demais dados cadastrados nunca são.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly dateAdapter = inject(DateAdapter);
  readonly lang = signal<AppLang>(storedLang());

  init(): void {
    this.apply(this.lang());
  }

  set(lang: AppLang): void {
    this.lang.set(lang);
    try { localStorage.setItem(KEY, lang); } catch { /* armazenamento indisponível */ }
    this.apply(lang);
  }

  private apply(lang: AppLang): void {
    this.transloco.setActiveLang(lang);
    this.dateAdapter.setLocale(lang);
    document.documentElement.lang = lang;
  }
}
