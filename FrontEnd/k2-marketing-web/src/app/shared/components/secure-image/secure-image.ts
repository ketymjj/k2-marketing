import { Component, OnDestroy, OnInit, inject, input, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { ApiService } from '../../../core/services/api.service';
import '../analytics.shared';

/** Imagem de criativo carregada com o token (o arquivo não tem URL pública). */
@Component({
  selector: 'k2-secure-img',
  imports: [MatIconModule],
  templateUrl: './secure-image.html',
  styleUrl: './secure-image.css'
})
export class SecureImageComponent implements OnInit, OnDestroy {
  readonly creativeId = input.required<string>();
  readonly alt = input('');
  readonly icon = input('image');
  readonly url = signal<string | null>(null);
  readonly failed = signal(false);
  private readonly api = inject(ApiService);
  private static readonly cache = new Map<string, string>();

  ngOnInit(): void {
    const cached = SecureImageComponent.cache.get(this.creativeId());
    if (cached) { this.url.set(cached); return; }
    this.api.blob(`/creatives/${this.creativeId()}/file`).subscribe({
      next: b => {
        const u = URL.createObjectURL(b);
        SecureImageComponent.cache.set(this.creativeId(), u);
        this.url.set(u);
      },
      error: () => this.failed.set(true)
    });
  }

  ngOnDestroy(): void { /* URLs ficam em cache durante a sessão para evitar novos downloads. */ }
}
