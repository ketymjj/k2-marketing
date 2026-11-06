import { signal } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { MatDialog } from '@angular/material/dialog';
import { Observable, Subscription, filter, map } from 'rxjs';
import { Paged } from './models/models';
import { Params } from '../core/services/api.service';
import { ConfirmData } from './components/common.shared';
import { ConfirmDialogComponent } from './components/confirm-dialog/confirm-dialog';

/** Estado de uma listagem paginada/ordenada no servidor. */
export class PagedList<T> {
  readonly items = signal<T[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly loaded = signal(false);
  page = 0;
  pageSize = 20;
  sortBy: string | undefined;
  sortDir: 'asc' | 'desc' | undefined;
  private sub?: Subscription;

  constructor(private readonly fetch: (params: Params) => Observable<Paged<T>>, private readonly filters: () => Params = () => ({})) {}

  load(resetPage = false): void {
    if (resetPage) this.page = 0;
    this.loading.set(true);
    this.sub?.unsubscribe();
    this.sub = this.fetch({ ...this.filters(), page: this.page + 1, pageSize: this.pageSize, sortBy: this.sortBy, sortDir: this.sortDir }).subscribe({
      next: r => { this.items.set(r.items); this.total.set(r.totalCount); this.loading.set(false); this.loaded.set(true); },
      error: () => { this.loading.set(false); this.loaded.set(true); }
    });
  }

  onPage(e: PageEvent): void {
    this.page = e.pageIndex;
    this.pageSize = e.pageSize;
    this.load();
  }

  onSort(s: Sort): void {
    this.sortBy = s.direction ? s.active : undefined;
    this.sortDir = s.direction || undefined;
    this.load(true);
  }
}

/** Abre o diálogo de confirmação e emite somente quando confirmado. */
export function confirm(dialog: MatDialog, data: ConfirmData): Observable<true> {
  return dialog.open(ConfirmDialogComponent, { data, width: '440px' }).afterClosed().pipe(filter(Boolean), map(() => true as const));
}

/** Debounce simples para campos de busca. */
export function debounce(fn: () => void, ms = 350): () => void {
  let t: ReturnType<typeof setTimeout> | undefined;
  return () => { clearTimeout(t); t = setTimeout(fn, ms); };
}
