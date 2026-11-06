import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule } from '@angular/material/sort';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatDialogModule } from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatBadgeModule } from '@angular/material/badge';
import { MatDividerModule } from '@angular/material/divider';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { TranslocoModule } from '@jsverse/transloco';
import { DtPipe, FileSizePipe, MoneyPipe, NumPipe, PctPipe, RoasPipe } from './pipes/format.pipes';
import { EmptyStateComponent } from './components/empty-state/empty-state';
import { PageHeaderComponent } from './components/page-header/page-header';
import { SkeletonComponent } from './components/skeleton/skeleton';
import { StatusChipComponent } from './components/status-chip/status-chip';
import { TipComponent } from './components/tip/tip';
import { FieldHelpComponent } from './components/field-help/field-help';

/** Imports comuns às telas (Material, formulários, tradução, pipes e componentes básicos). */
export const UI = [
  CommonModule, ReactiveFormsModule, FormsModule, RouterLink, TranslocoModule,
  MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule, MatSortModule,
  MatPaginatorModule, MatDialogModule, MatDatepickerModule, MatTooltipModule, MatMenuModule, MatSlideToggleModule,
  MatTabsModule, MatProgressBarModule, MatCheckboxModule, MatButtonToggleModule, MatBadgeModule, MatDividerModule, MatAutocompleteModule,
  MoneyPipe, DtPipe, NumPipe, PctPipe, RoasPipe, FileSizePipe,
  PageHeaderComponent, StatusChipComponent, EmptyStateComponent, SkeletonComponent, TipComponent, FieldHelpComponent
] as const;
