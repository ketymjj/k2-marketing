import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoModule } from '@jsverse/transloco';

/** Navegação interna da Central de Aprendizado. */
@Component({
  selector: 'k2-learn-nav',
  imports: [RouterLink, RouterLinkActive, MatIconModule, TranslocoModule],
  templateUrl: './learn-nav.html',
  styleUrl: './learn-nav.css'
})
export class LearnNavComponent {
  readonly links = [
    { route: '/learn', icon: 'menu_book', label: 'learn.nav.overview', exact: true },
    { route: '/learn/examples', icon: 'lightbulb', label: 'learn.nav.examples', exact: false },
    { route: '/learn/simulator', icon: 'calculate', label: 'learn.nav.simulator', exact: false },
    { route: '/learn/glossary', icon: 'spellcheck', label: 'learn.nav.glossary', exact: false }
  ];
}
