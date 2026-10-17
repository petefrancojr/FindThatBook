import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { BookSearchResponse, BookSearchService } from '../../api/generated';

@Component({
  imports: [],
  selector: 'app-search',
  styleUrl: './search.css',
  templateUrl: './search.html',
})
export class Search {
  private readonly bookSearch = inject(BookSearchService);

  protected readonly maxQueryLength = 200;
  protected readonly query = signal('');
  protected readonly loading = signal(false);
  protected readonly response = signal<BookSearchResponse | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly canSearch = computed(() => this.query().trim().length > 0 && !this.loading());
  protected onInput(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  protected async search(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.canSearch()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    try {
      this.response.set(await this.bookSearch.searchBooks({ q: this.query().trim() }));
    } catch (err) {
      this.response.set(null);
      this.error.set(this.describeError(err));
    } finally {
      this.loading.set(false);
    }
  }

  private describeError(err: unknown): string {
    if (err instanceof HttpErrorResponse && err.status === 400) {
      const messages = Object.values<string[]>(err.error?.errors ?? {}).flat();
      return messages[0] ?? 'Check your search and try again.';
    }
    return 'Search is unavailable right now. Try again in a moment.';
  }
}
