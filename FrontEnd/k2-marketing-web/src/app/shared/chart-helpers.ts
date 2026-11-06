import { ChartConfiguration } from 'chart.js';
import { parseIsoDate } from '../core/services/api.service';

/** Rótulos curtos de data (dd/MMM) no idioma ativo. */
export function dayLabels(dates: string[], lang: string): string[] {
  const fmt = new Intl.DateTimeFormat(lang, { day: '2-digit', month: 'short' });
  return dates.map(d => fmt.format(parseIsoDate(d)));
}

export function moneyTick(lang: string) {
  const fmt = new Intl.NumberFormat(lang, { style: 'currency', currency: 'BRL', notation: 'compact', maximumFractionDigits: 1 });
  return (v: number | string) => fmt.format(Number(v));
}

export function moneyFmt(lang: string, v: number): string {
  return new Intl.NumberFormat(lang, { style: 'currency', currency: 'BRL' }).format(v);
}

export function lineConfig(labels: string[], datasets: { label: string; data: (number | null)[]; color: string; fill?: boolean; yAxisID?: string; dashed?: boolean }[],
  options: ChartConfiguration['options'] = {}): ChartConfiguration {
  return {
    type: 'line',
    data: {
      labels,
      datasets: datasets.map(d => ({
        label: d.label, data: d.data, borderColor: d.color, backgroundColor: d.color + '22', fill: d.fill ?? false, tension: 0.35,
        pointRadius: 0, pointHoverRadius: 4, borderWidth: 2, yAxisID: d.yAxisID, borderDash: d.dashed ? [6, 4] : undefined
      }))
    },
    options
  };
}

export function barConfig(labels: string[], datasets: { label: string; data: (number | null)[]; color: string | string[] }[],
  options: ChartConfiguration['options'] = {}, horizontal = false): ChartConfiguration {
  return {
    type: 'bar',
    data: {
      labels,
      datasets: datasets.map(d => ({ label: d.label, data: d.data, backgroundColor: d.color, borderRadius: 6, maxBarThickness: horizontal ? 22 : 34 }))
    },
    options: { indexAxis: horizontal ? 'y' : 'x', ...options }
  };
}

export function doughnutConfig(labels: string[], data: number[], colors: string[], options: ChartConfiguration['options'] = {}): ChartConfiguration {
  return {
    type: 'doughnut',
    data: { labels, datasets: [{ data, backgroundColor: colors, borderWidth: 0, hoverOffset: 6 }] },
    options: { cutout: '68%', plugins: { legend: { position: 'bottom' } }, ...options } as ChartConfiguration['options']
  };
}
