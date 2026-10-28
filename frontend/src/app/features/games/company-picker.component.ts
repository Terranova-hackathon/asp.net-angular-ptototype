import { Component, inject, input, output, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { CompanyKind, CompanyOption } from './games.models';
import { GamesService } from './games.service';

const MAX_SELECTED = 10;

/** Type-ahead for studios or publishers. The list is far too big for a dropdown, so it is searched on the server. */
@Component({
  selector: 'app-company-picker',
  template: `
    <label class="field">
      <span>{{ label() }}</span>
      <input
        type="search"
        autocomplete="off"
        placeholder="Type at least 2 letters…"
        [value]="term()"
        [disabled]="selected().length >= max"
        (input)="term.set($any($event.target).value)"
      />
    </label>

    @if (suggestions().length) {
      <ul class="suggestions">
        @for (c of suggestions(); track c.slug) {
          <li>
            <button type="button" (click)="add(c)" [disabled]="selected().includes(c.slug)">
              {{ c.name }} <span class="muted">({{ c.gamesCount }})</span>
            </button>
          </li>
        }
      </ul>
    }

    @if (selected().length) {
      <div class="chips">
        @for (slug of selected(); track slug) {
          <span class="chip">
            {{ slug.replaceAll('-', ' ') }}
            <button type="button" [attr.aria-label]="'Remove ' + slug" (click)="remove(slug)">×</button>
          </span>
        }
      </div>
    }
  `,
})
export class CompanyPickerComponent {
  private readonly games = inject(GamesService);

  readonly kind = input.required<CompanyKind>();
  readonly label = input.required<string>();
  readonly selected = input<string[]>([]);
  readonly selectedChange = output<string[]>();

  protected readonly max = MAX_SELECTED;
  protected readonly term = signal('');

  protected readonly suggestions = toSignal(
    toObservable(this.term).pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap((term) =>
        term.trim().length < 2
          ? of<CompanyOption[]>([])
          : this.games.searchCompanies(this.kind(), term).pipe(catchError(() => of<CompanyOption[]>([]))),
      ),
    ),
    { initialValue: [] as CompanyOption[] },
  );

  protected add(company: CompanyOption): void {
    if (!this.selected().includes(company.slug) && this.selected().length < MAX_SELECTED) {
      this.selectedChange.emit([...this.selected(), company.slug]);
    }
    this.term.set('');
  }

  protected remove(slug: string): void {
    this.selectedChange.emit(this.selected().filter((s) => s !== slug));
  }
}
