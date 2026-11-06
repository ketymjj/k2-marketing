import { Component } from '@angular/core';
import { UI } from '../../../shared/ui';

/** Documentação da origem dos números e da metodologia de cálculo (exibida para todos os perfis). */
@Component({
  imports: [UI],
  templateUrl: './methodology-page.html',
  styleUrl: './methodology-page.css'
})
export class MethodologyPage {
  readonly formulas = [{ k: 'CTR' }, { k: 'CPC' }, { k: 'CPM' }, { k: 'CPL' }, { k: 'CPA' }, { k: 'ROAS' }];
  readonly sources = ['Manual', 'CsvImport', 'Demo', 'Integration'];
  readonly rules = ['spend', 'budget', 'leads', 'conversions', 'dedup', 'metricsUnique', 'reach', 'revenue', 'comparison'];
}
