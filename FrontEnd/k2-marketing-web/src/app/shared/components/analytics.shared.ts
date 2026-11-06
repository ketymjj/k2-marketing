import { ArcElement, BarController, BarElement, CategoryScale, Chart, DoughnutController, Filler, Legend, LineController, LineElement, LinearScale, PointElement, Tooltip } from 'chart.js';
import { AdPlatform } from '../models/models';

Chart.register(LineController, LineElement, PointElement, BarController, BarElement, DoughnutController, ArcElement,
  CategoryScale, LinearScale, Tooltip, Legend, Filler);
Chart.defaults.font.family = "'Inter', 'Segoe UI', system-ui, sans-serif";
/** Paleta da marca para séries de gráficos. */
export const PALETTE = {
  purple: '#6941C6', blue: '#2E90FA', green: '#12B76A', navy: '#344054', orange: '#F79009', red: '#F04438',
  pink: '#EE46BC', teal: '#15B79E', gray: '#98A2B3', indigo: '#444CE7'
};
export const SERIES = [PALETTE.purple, PALETTE.blue, PALETTE.green, PALETTE.orange, PALETTE.pink, PALETTE.teal, PALETTE.indigo, PALETTE.red];
export const PLATFORM_COLORS: Record<string, string> = {
  MetaAds: '#2E90FA', GoogleAds: '#12B76A', TikTokAds: '#EE46BC', LinkedInAds: '#444CE7', Other: '#98A2B3'
};
export interface AnalyticsFilter { clientId: string | null; platform: AdPlatform | null; campaignId: string | null; from: string | null; to: string | null; }
export type Preset = '7d' | '30d' | '90d' | 'month' | 'lastMonth' | 'custom';
