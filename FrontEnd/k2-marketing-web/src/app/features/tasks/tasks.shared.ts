import { MarketingTask } from '../../shared/models/models';

export interface Day { date: Date; iso: string; inMonth: boolean; today: boolean; tasks: MarketingTask[]; }
