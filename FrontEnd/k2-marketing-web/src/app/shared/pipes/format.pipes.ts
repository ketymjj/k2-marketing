import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from '../../core/i18n/i18n';
import { parseIsoDate } from '../../core/services/api.service';

/** Valores monetários em BRL, formatados conforme o idioma ativo. Nulo → "—". */
@Pipe({ name: 'money', pure: false })
export class MoneyPipe implements PipeTransform {
  private readonly lang = inject(LanguageService);
  transform(value: number | null | undefined, digits = 2): string {
    if (value === null || value === undefined) return '—';
    return new Intl.NumberFormat(this.lang.lang(), { style: 'currency', currency: 'BRL', minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);
  }
}

/** Datas de calendário (yyyy-MM-dd) ou instantes ISO. Formatos: date | short | datetime | time | month. */
@Pipe({ name: 'dt', pure: false })
export class DtPipe implements PipeTransform {
  private readonly lang = inject(LanguageService);
  transform(value: string | null | undefined, format: 'date' | 'short' | 'datetime' | 'time' | 'month' = 'date'): string {
    if (!value) return '—';
    const d = value.length === 10 ? parseIsoDate(value) : new Date(value);
    const opts: Intl.DateTimeFormatOptions =
      format === 'short' ? { day: '2-digit', month: 'short' } :
      format === 'datetime' ? { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' } :
      format === 'time' ? { hour: '2-digit', minute: '2-digit' } :
      format === 'month' ? { month: 'long', year: 'numeric' } :
      { day: '2-digit', month: '2-digit', year: 'numeric' };
    return new Intl.DateTimeFormat(this.lang.lang(), opts).format(d);
  }
}

/** Números com separador de milhar; `compact` abrevia (1,2 mi). Nulo → "—". */
@Pipe({ name: 'num', pure: false })
export class NumPipe implements PipeTransform {
  private readonly lang = inject(LanguageService);
  transform(value: number | null | undefined, digits = 0, compact = false): string {
    if (value === null || value === undefined) return '—';
    return new Intl.NumberFormat(this.lang.lang(), compact
      ? { notation: 'compact', maximumFractionDigits: 1 }
      : { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);
  }
}

/** Percentual já calculado em pontos (ex.: 2.35 → "2,35%"). */
@Pipe({ name: 'pct', pure: false })
export class PctPipe implements PipeTransform {
  private readonly lang = inject(LanguageService);
  transform(value: number | null | undefined, digits = 2): string {
    if (value === null || value === undefined) return '—';
    return new Intl.NumberFormat(this.lang.lang(), { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value) + '%';
  }
}

/** ROAS como multiplicador (ex.: 3.2 → "3,20x"). */
@Pipe({ name: 'roas', pure: false })
export class RoasPipe implements PipeTransform {
  private readonly lang = inject(LanguageService);
  transform(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return new Intl.NumberFormat(this.lang.lang(), { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value) + 'x';
  }
}

@Pipe({ name: 'fileSize' })
export class FileSizePipe implements PipeTransform {
  transform(bytes: number | null | undefined): string {
    if (!bytes) return '—';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }
}

/** Variação percentual entre dois valores (null quando o anterior é zero). */
export function delta(current: number | null | undefined, previous: number | null | undefined): number | null {
  if (current === null || current === undefined || !previous) return null;
  return ((current - previous) / previous) * 100;
}
