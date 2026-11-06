import { AfterViewInit, Component, ElementRef, OnDestroy, effect, inject, input, viewChild } from '@angular/core';
import { Chart, ChartConfiguration } from 'chart.js';
import { ThemeService } from '../../../core/services/theme.service';
import '../analytics.shared';

/** Wrapper do Chart.js: recria o gráfico quando a configuração, o idioma ou o tema mudam. */
@Component({
  selector: 'k2-chart',
  templateUrl: './chart.html',
  styleUrl: './chart.css'
})
export class ChartComponent implements AfterViewInit, OnDestroy {
  readonly config = input.required<ChartConfiguration>();
  readonly height = input(280);
  private readonly theme = inject(ThemeService);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private chart: Chart | null = null;
  private ready = false;

  constructor() {
    effect(() => {
      const cfg = this.config();
      this.theme.theme();
      if (this.ready) this.render(cfg);
    });
  }

  ngAfterViewInit(): void {
    this.ready = true;
    this.render(this.config());
  }

  /** Imagem PNG do gráfico (usada no relatório PDF). */
  toImage(): string | null {
    return this.chart?.toBase64Image('image/png', 1) ?? null;
  }

  private render(cfg: ChartConfiguration): void {
    const dark = this.theme.theme() === 'dark';
    const text = dark ? '#cecfd2' : '#475467';
    const grid = dark ? 'rgba(255,255,255,0.07)' : 'rgba(16,24,40,0.06)';
    Chart.defaults.color = text;
    Chart.defaults.borderColor = grid;
    this.chart?.destroy();
    const isRadial = cfg.type === 'doughnut' || cfg.type === 'pie';
    this.chart = new Chart(this.canvas().nativeElement, {
      ...cfg,
      options: {
        responsive: true, maintainAspectRatio: false, animation: { duration: 400 },
        interaction: { mode: isRadial ? 'nearest' : 'index', intersect: isRadial },
        plugins: {
          legend: { labels: { usePointStyle: true, boxWidth: 8, padding: 14, color: text } },
          tooltip: { backgroundColor: dark ? '#1f242f' : '#101828', padding: 10, cornerRadius: 8, titleFont: { weight: 600 } },
          ...(cfg.options?.plugins ?? {})
        },
        ...(isRadial ? {} : {
          scales: {
            x: { grid: { display: false }, ticks: { color: text, maxRotation: 0, autoSkipPadding: 12 } },
            y: { grid: { color: grid }, ticks: { color: text }, beginAtZero: true }
          }
        }),
        ...cfg.options,
        ...(cfg.options?.scales ? { scales: cfg.options.scales } : {})
      }
    } as ChartConfiguration);
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }
}
