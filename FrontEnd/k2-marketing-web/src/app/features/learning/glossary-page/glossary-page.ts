import { Component, computed, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { UI } from '../../../shared/ui';
import { LanguageService } from '../../../core/i18n/i18n';
import { LearnNavComponent } from '../learn-nav/learn-nav';
import { GLOSSARY_KEYS } from '../learning.shared';

interface Term { key: string; name: string; english: string; definition: string; example: string; letter: string; }

const normalize = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

/** Glossário pesquisável: busca no idioma ativo e também pelo termo em inglês. */
@Component({
  imports: [UI, LearnNavComponent],
  templateUrl: './glossary-page.html',
  styleUrl: './glossary-page.css'
})
export class GlossaryPage {
  private readonly t = inject(TranslocoService);
  private readonly lang = inject(LanguageService);
  readonly query = signal('');
  readonly letter = signal<string | null>(null);

  private readonly terms = computed<Term[]>(() => {
    this.lang.lang();
    return GLOSSARY_KEYS.map(key => {
      const name = this.t.translate(`learn.glossary.terms.${key}.name`);
      return {
        key, name,
        english: this.t.translate(`learn.glossary.terms.${key}.en`),
        definition: this.t.translate(`learn.glossary.terms.${key}.definition`),
        example: this.t.translate(`learn.glossary.terms.${key}.example`),
        letter: normalize(name).charAt(0).toUpperCase()
      };
    }).sort((a, b) => a.name.localeCompare(b.name, this.lang.lang()));
  });

  readonly letters = computed(() => [...new Set(this.terms().map(t => t.letter))]);

  readonly filtered = computed(() => {
    const q = normalize(this.query().trim());
    return this.terms().filter(t =>
      (!this.letter() || t.letter === this.letter()) &&
      (!q || [t.name, t.english, t.definition].some(v => normalize(v).includes(q))));
  });

  setQuery(v: string): void { this.query.set(v); this.letter.set(null); }
  toggleLetter(l: string): void { this.letter.set(this.letter() === l ? null : l); }
}
