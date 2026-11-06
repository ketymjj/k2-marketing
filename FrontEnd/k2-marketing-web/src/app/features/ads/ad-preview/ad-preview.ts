import { Component, computed, input } from '@angular/core';
import { UI } from '../../../shared/ui';
import { SecureImageComponent } from '../../../shared/components/secure-image/secure-image';
import { LinkedCreative } from '../../../shared/models/models';

/** Prévia do anúncio em formato de post de feed (os textos são exibidos exatamente como cadastrados). */
@Component({
  selector: 'k2-ad-preview',
  imports: [UI, SecureImageComponent],
  templateUrl: './ad-preview.html',
  styleUrl: './ad-preview.css'
})
export class AdPreviewComponent {
  readonly clientName = input('');
  readonly headline = input('');
  readonly primaryText = input('');
  readonly description = input<string | null>('');
  readonly callToAction = input('');
  readonly destinationUrl = input('');
  readonly creatives = input<LinkedCreative[]>([]);
  readonly visual = computed(() => this.creatives().find(c => c.type !== 'Text') ?? null);
  readonly domain = computed(() => { try { return new URL(this.destinationUrl()).hostname.toUpperCase(); } catch { return ''; } });
}
