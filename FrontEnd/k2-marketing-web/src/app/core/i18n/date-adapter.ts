import { Injectable } from '@angular/core';
import { NativeDateAdapter } from '@angular/material/core';

/**
 * Interpreta datas digitadas conforme o idioma ativo: dd/MM/aaaa em pt-BR e MM/dd/aaaa em en-US
 * (o NativeDateAdapter usa Date.parse, que não entende o formato brasileiro). Aceita também aaaa-MM-dd.
 */
@Injectable()
export class LocaleDateAdapter extends NativeDateAdapter {
  override parse(value: unknown, parseFormat?: unknown): Date | null {
    if (typeof value !== 'string') return super.parse(value, parseFormat);
    const text = value.trim();
    if (!text) return null;
    const iso = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(text);
    if (iso) return this.build(+iso[1], +iso[2], +iso[3]);
    const parts = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{2,4})$/.exec(text);
    if (!parts) return this.invalid();
    const [a, b, y] = [+parts[1], +parts[2], +parts[3]];
    const year = y < 100 ? 2000 + y : y;
    return this.locale?.toString().startsWith('en') ? this.build(year, a, b) : this.build(year, b, a);
  }

  private build(year: number, month: number, day: number): Date {
    const d = new Date(year, month - 1, day);
    return d.getFullYear() === year && d.getMonth() === month - 1 && d.getDate() === day ? d : this.invalid();
  }
}
