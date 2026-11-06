import { Metrics } from '../../shared/models/models';

export interface Row { key: keyof Metrics; fmt: 'money' | 'num' | 'pct' | 'roas'; }
