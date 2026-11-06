// Conteúdo e cálculos do módulo educativo. Tudo roda no navegador: nada aqui grava dados na API.

/** Valores informados de uma campanha (reais, de exemplo ou do simulador). */
export interface MetricInputs {
  spend: number; impressions: number; reach?: number; clicks: number; leads: number; conversions: number; revenue: number;
}

export interface CalculatedMetrics { ctr: number | null; cpc: number | null; cpm: number | null; cpl: number | null; cpa: number | null; roas: number | null; }

const round = (v: number, digits = 4) => Math.round(v * 10 ** digits) / 10 ** digits;

/**
 * Mesmas fórmulas do backend (MetricCalculator): denominador zero → null ("—"), nunca 0 ou infinito.
 */
export function calculate(m: MetricInputs): CalculatedMetrics {
  const div = (a: number, b: number, k = 1) => (b > 0 ? round((a / b) * k) : null);
  return {
    ctr: div(m.clicks, m.impressions, 100),
    cpc: div(m.spend, m.clicks),
    cpm: div(m.spend, m.impressions, 1000),
    cpl: div(m.spend, m.leads),
    cpa: div(m.spend, m.conversions),
    roas: div(m.revenue, m.spend)
  };
}

/** Indicadores com explicação (chaves de learn.metrics.*). */
export const METRIC_KEYS = ['impressions', 'reach', 'clicks', 'ctr', 'cpc', 'cpm', 'leads', 'cpl', 'conversions', 'cpa', 'revenue', 'roas'] as const;
export type MetricKey = typeof METRIC_KEYS[number];

/** Mapeia o rótulo do KPI (ex.: 'kpi.ctr', 'kpi.spend') para a explicação correspondente. */
export function metricFromLabel(label: string): MetricKey | 'spend' | null {
  const key = label.startsWith('kpi.') ? label.slice(4) : null;
  if (!key) return null;
  if (key === 'spend') return 'spend';
  return (METRIC_KEYS as readonly string[]).includes(key) ? key as MetricKey : null;
}

export const TOPICS = [
  { key: 'intro', icon: 'school' },
  { key: 'campaign', icon: 'campaign' },
  { key: 'ad', icon: 'ad_units' },
  { key: 'creatives', icon: 'photo_library' },
  { key: 'paidTraffic', icon: 'traffic' },
  { key: 'budget', icon: 'account_balance_wallet' },
  { key: 'leads', icon: 'people_alt' },
  { key: 'conversions', icon: 'shopping_cart_checkout' },
  { key: 'results', icon: 'insights' },
  { key: 'report', icon: 'summarize' }
] as const;

export const GLOSSARY_KEYS = [
  'campaign', 'ad', 'creative', 'paidTraffic', 'impression', 'reach', 'click', 'ctr', 'cpc', 'cpm', 'cpl', 'cpa', 'roas',
  'lead', 'conversion', 'audience', 'remarketing', 'funnel', 'landingPage', 'pixel', 'dailyBudget'
] as const;

/** Exemplos educativos com dados fictícios (não existem no banco). */
export interface LearningExample {
  key: 'bakery' | 'salon' | 'realEstate';
  icon: string;
  platforms: string;
  data: Required<MetricInputs> & { sales?: number };
}

export const EXAMPLES: LearningExample[] = [
  { key: 'bakery', icon: 'bakery_dining', platforms: 'Instagram + Facebook',
    data: { spend: 500, impressions: 10_000, reach: 6_500, clicks: 400, leads: 40, conversions: 10, revenue: 1_250 } },
  { key: 'salon', icon: 'content_cut', platforms: 'Instagram',
    data: { spend: 300, impressions: 8_000, reach: 5_200, clicks: 240, leads: 36, conversions: 18, revenue: 1_620 } },
  { key: 'realEstate', icon: 'holiday_village', platforms: 'Facebook + Instagram',
    data: { spend: 3_000, impressions: 60_000, reach: 32_000, clicks: 1_500, leads: 75, conversions: 2, revenue: 180_000, sales: 2 } }
];
