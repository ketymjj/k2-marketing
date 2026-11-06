import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { EXAMPLES, calculate, metricFromLabel } from './learning.shared';
import { LearningService } from '../../core/services/learning.service';
import { TOUR_STEPS, TourService } from '../../core/services/tour.service';

describe('Simulador — calculate()', () => {
  it('calcula os indicadores do exemplo da padaria', () => {
    const r = calculate({ spend: 500, impressions: 10_000, clicks: 400, leads: 40, conversions: 10, revenue: 1_250 });
    expect(r.ctr).toBe(4);
    expect(r.cpc).toBe(1.25);
    expect(r.cpm).toBe(50);
    expect(r.cpl).toBe(12.5);
    expect(r.cpa).toBe(50);
    expect(r.roas).toBe(2.5);
  });

  it('reproduz o exemplo do CTR: 50 cliques em 1.000 impressões = 5%', () => {
    expect(calculate({ spend: 0, impressions: 1_000, clicks: 50, leads: 0, conversions: 0, revenue: 0 }).ctr).toBe(5);
  });

  it('retorna null quando o denominador é zero (nunca 0 ou Infinity)', () => {
    const r = calculate({ spend: 100, impressions: 0, clicks: 0, leads: 0, conversions: 0, revenue: 300 });
    expect(r.ctr).toBeNull();
    expect(r.cpc).toBeNull();
    expect(r.cpm).toBeNull();
    expect(r.cpl).toBeNull();
    expect(r.cpa).toBeNull();
    expect(r.roas).toBe(3);
    expect(calculate({ spend: 0, impressions: 10, clicks: 1, leads: 0, conversions: 0, revenue: 50 }).roas).toBeNull();
  });

  it('arredonda em 4 casas como o backend', () => {
    expect(calculate({ spend: 300, impressions: 8_000, clicks: 240, leads: 36, conversions: 18, revenue: 1_620 }).cpl).toBe(8.3333);
  });

  it('os exemplos educativos têm números coerentes', () => {
    for (const e of EXAMPLES) {
      expect(e.data.clicks).toBeLessThanOrEqual(e.data.impressions);
      expect(e.data.reach).toBeLessThanOrEqual(e.data.impressions);
      expect(e.data.conversions).toBeLessThanOrEqual(e.data.leads);
    }
    const [bakery, salon, realEstate] = EXAMPLES.map(e => calculate(e.data));
    expect(salon.ctr).toBe(3);
    expect(salon.roas).toBe(5.4);
    expect(realEstate.cpc).toBe(2);
    expect(realEstate.cpa).toBe(1_500);
    expect(bakery.cpa).toBe(50);
  });
});

describe('metricFromLabel()', () => {
  it('mapeia rótulos de KPI para explicações', () => {
    expect(metricFromLabel('kpi.ctr')).toBe('ctr');
    expect(metricFromLabel('kpi.spend')).toBe('spend');
    expect(metricFromLabel('kpi.activeClients')).toBeNull();
    expect(metricFromLabel('budgets.planned')).toBeNull();
  });
});

describe('LearningService — Modo Iniciante', () => {
  beforeEach(() => localStorage.removeItem('k2m.beginner'));
  afterEach(() => localStorage.removeItem('k2m.beginner'));

  it('começa desligado e persiste a preferência no localStorage', () => {
    const s = new LearningService();
    expect(s.beginner()).toBeFalse();
    s.setBeginner(true);
    expect(localStorage.getItem('k2m.beginner')).toBe('1');
    expect(new LearningService().beginner()).toBeTrue();
    s.toggle();
    expect(s.beginner()).toBeFalse();
    expect(new LearningService().beginner()).toBeFalse();
  });
});

@Component({ template: '' })
class BlankPage {}

describe('TourService', () => {
  let tour: TourService;
  let router: Router;

  beforeEach(() => {
    localStorage.removeItem('k2m.tourDone');
    const routes = [...new Set(TOUR_STEPS.map(s => s.route.slice(1)))].map(path => ({ path, component: BlankPage }));
    TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
    tour = TestBed.inject(TourService);
    router = TestBed.inject(Router);
  });

  afterEach(() => localStorage.removeItem('k2m.tourDone'));

  it('tem as 10 etapas pedidas, na ordem', () => {
    expect(TOUR_STEPS.map(s => s.key)).toEqual(['dashboard', 'client', 'campaign', 'budget', 'ad', 'creative', 'results', 'metrics', 'lead', 'report']);
  });

  it('avança, volta, navega para a tela da etapa e pode ser pulado', async () => {
    tour.start();
    expect(tour.active()).toBeTrue();
    expect(tour.index()).toBe(0);
    tour.next();
    tour.next();
    expect(tour.index()).toBe(2);
    await new Promise(r => setTimeout(r));
    expect(router.url).toBe('/campaigns');
    tour.prev();
    expect(tour.index()).toBe(1);
    tour.skip();
    expect(tour.active()).toBeFalse();
    expect(tour.done).toBeFalse();
  });

  it('finalizar marca o tour como concluído e permite reiniciar', () => {
    tour.start();
    for (let i = 0; i < TOUR_STEPS.length; i++) tour.next(); // a última chamada finaliza
    expect(tour.active()).toBeFalse();
    expect(tour.done).toBeTrue();
    tour.start();
    expect(tour.active()).toBeTrue();
    expect(tour.index()).toBe(0);
  });
});
