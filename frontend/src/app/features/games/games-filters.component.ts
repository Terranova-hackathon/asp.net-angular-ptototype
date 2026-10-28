import { Component, computed, input, output } from '@angular/core';
import { CompanyPickerComponent } from './company-picker.component';
import { GameFilters, NO_FILTERS, activeFilterCount } from './games.filters';
import { GameFilterOptions } from './games.models';

const METACRITIC_STEPS = [50, 60, 70, 80, 90];

@Component({
  selector: 'app-games-filters',
  imports: [CompanyPickerComponent],
  template: `
    <details class="filters card" [open]="activeCount() > 0">
      <summary>
        Filters @if (activeCount() > 0) { <span class="badge">{{ activeCount() }}</span> }
      </summary>

      @if (options(); as opts) {
        <div class="filter-grid">
          <fieldset>
            <legend>Genres <span class="muted">(any of)</span></legend>
            @for (g of opts.genres; track g.value) {
              <label class="check">
                <input
                  type="checkbox"
                  [checked]="value().genres.includes(g.value)"
                  (change)="toggle('genres', g.value)"
                />
                {{ g.name }}
              </label>
            }
          </fieldset>

          <fieldset>
            <legend>Platforms <span class="muted">(any of)</span></legend>
            @for (p of opts.platforms; track p.value) {
              <label class="check">
                <input
                  type="checkbox"
                  [checked]="value().platforms.includes(p.value)"
                  (change)="toggle('platforms', p.value)"
                />
                {{ p.name }}
              </label>
            }
          </fieldset>

          <div>
            <label class="field">
              <span>Sort by</span>
              <select [value]="value().ordering" (change)="patch({ ordering: $any($event.target).value })">
                <option value="">Best match</option>
                @for (o of opts.orderings; track o.value) {
                  <option [value]="o.value" [selected]="value().ordering === o.value">{{ o.name }}</option>
                }
              </select>
            </label>

            <label class="field">
              <span>Minimum Metacritic</span>
              <select
                (change)="patch({ metacriticMin: $any($event.target).value === '' ? null : +$any($event.target).value })"
              >
                <option value="" [selected]="value().metacriticMin === null">Any</option>
                @for (m of metacriticSteps; track m) {
                  <option [value]="m" [selected]="value().metacriticMin === m">{{ m }}+</option>
                }
              </select>
            </label>
          </div>

          <div>
            <app-company-picker
              kind="developer"
              label="Studio"
              [selected]="value().developers"
              (selectedChange)="patch({ developers: $event })"
            />
            <app-company-picker
              kind="publisher"
              label="Publisher"
              [selected]="value().publishers"
              (selectedChange)="patch({ publishers: $event })"
            />
          </div>
        </div>
      } @else {
        <p class="muted">Loading filters…</p>
      }

      @if (activeCount() > 0) {
        <button type="button" class="link" (click)="changed.emit(none)">Clear all filters</button>
      }
    </details>
  `,
})
export class GamesFiltersComponent {
  readonly options = input<GameFilterOptions | null>(null);
  readonly value = input.required<GameFilters>();
  readonly changed = output<GameFilters>();

  protected readonly none = NO_FILTERS;
  protected readonly metacriticSteps = METACRITIC_STEPS;
  protected readonly activeCount = computed(() => activeFilterCount(this.value()));

  protected toggle(key: 'genres' | 'platforms', item: string): void {
    const current = this.value()[key];
    this.patch({ [key]: current.includes(item) ? current.filter((i) => i !== item) : [...current, item] });
  }

  protected patch(change: Partial<GameFilters>): void {
    this.changed.emit({ ...this.value(), ...change });
  }
}
