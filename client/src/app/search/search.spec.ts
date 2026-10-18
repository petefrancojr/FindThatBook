import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BookResult, BookSearchResponse, BookSearchService } from '../../api/generated';
import { Search } from './search';

// Tests are grouped by the front-end requirements in the project brief
// (see "Return results" and "Error handling and edge cases").
describe('Search', () => {
  let fixture: ComponentFixture<Search>;
  let searchBooks: ReturnType<typeof vi.fn>;

  const hobbit: BookResult = {
    title: 'The Hobbit',
    author: 'J.R.R. Tolkien',
    first_publish_year: 1937,
    open_library_id: 'OL27482W',
    url: 'https://openlibrary.org/works/OL27482W',
    cover_url: 'https://covers.openlibrary.org/b/id/6979861-M.jpg',
    tier: 'A',
    explanation: 'Exact title match; Tolkien is primary author; Dixon listed as adaptor.',
  };

  const graphicNovel: BookResult = {
    title: 'The Hobbit: Graphic Novel',
    author: 'J.R.R. Tolkien',
    open_library_id: 'OL5735363W',
    url: 'https://openlibrary.org/works/OL5735363W',
    tier: 'B',
    explanation: 'Title contains "The Hobbit"; Tolkien is primary author.',
  };

  const response = (results: BookResult[], query = 'tolkien hobbit'): BookSearchResponse => ({
    query,
    results,
  });

  beforeEach(async () => {
    searchBooks = vi.fn();

    await TestBed.configureTestingModule({
      imports: [Search],
      providers: [{ provide: BookSearchService, useValue: { searchBooks } }],
    }).compileComponents();

    fixture = TestBed.createComponent(Search);
    await fixture.whenStable();
  });

  const page = () => fixture.nativeElement as HTMLElement;
  const input = () => page().querySelector('input') as HTMLInputElement;
  const button = () => page().querySelector('button') as HTMLButtonElement;
  const results = () => Array.from(page().querySelectorAll<HTMLElement>('.results > li'));
  const error = () => page().querySelector('.error')?.textContent?.trim();

  async function type(text: string) {
    input().value = text;
    input().dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  // Fires the form's submit event directly. Used where the button is disabled
  // and can't be clicked, to check the component's own guard.
  async function submit() {
    page().querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  async function clickSearch() {
    button().click();
    await fixture.whenStable();
  }

  async function searchFor(text: string) {
    await type(text);
    await clickSearch();
  }

  const metaItems = (li: HTMLElement) =>
    Array.from(li.querySelectorAll('.meta span')).map((span) => span.textContent?.trim());

  it('shows the Book Search heading', () => {
    expect(page().querySelector('h1')?.textContent).toBe('Book Search');
  });

  describe('one free-text search box', () => {
    it('has a single labelled search field rather than separate title and author fields', () => {
      const form = page().querySelector('form[role="search"]')!;
      expect(form.querySelectorAll('input, select, textarea')).toHaveLength(1);
      expect(input().type).toBe('search');
      expect(form.contains(input())).toBe(true);

      const label = page().querySelector(`label[for="${input().id}"]`);
      expect(label?.textContent?.trim()).toBe('Title, author or keywords');
    });

    it('sends messy text to the API as typed, only trimmed', async () => {
      searchBooks.mockResolvedValue(response([hobbit]));

      await searchFor('  tolkien hobbit illustrated deluxe 1937  ');

      expect(searchBooks).toHaveBeenCalledWith({ q: 'tolkien hobbit illustrated deluxe 1937' });
    });

    it('passes accents and punctuation through unchanged and shows them back correctly', async () => {
      const messy = 'Brontë — "jane eyre"?! & <co>';
      searchBooks.mockResolvedValue(response([], messy));

      await searchFor(messy);

      expect(searchBooks).toHaveBeenCalledWith({ q: messy });
      expect(page().querySelector('.empty')?.textContent?.trim()).toBe(`No books matched “${messy}”.`);
    });

    it('limits input to 200 characters, matching the API', () => {
      expect(input().getAttribute('maxlength')).toBe('200');
    });
  });

  describe('search button', () => {
    it('is a submit button inside the search form, so Enter in the field also searches', () => {
      const form = page().querySelector('form')!;
      expect(button().type).toBe('submit');
      expect(form.contains(button())).toBe(true);
      expect(button().textContent?.trim()).toBe('Search');
    });

    it('calls the search API when clicked', async () => {
      searchBooks.mockResolvedValue(response([hobbit]));

      await type('tolkien hobbit');
      expect(searchBooks).not.toHaveBeenCalled();

      await clickSearch();

      expect(searchBooks).toHaveBeenCalledTimes(1);
      expect(searchBooks).toHaveBeenCalledWith({ q: 'tolkien hobbit' });
      expect(results()).toHaveLength(1);
    });

    it('does nothing when clicked while disabled', async () => {
      await clickSearch();

      expect(searchBooks).not.toHaveBeenCalled();
    });
  });

  describe('empty search', () => {
    it('disables the button until something other than spaces is typed', async () => {
      expect(button().disabled).toBe(true);

      await type('   ');
      expect(button().disabled).toBe(true);

      await type('austen');
      expect(button().disabled).toBe(false);
    });

    it('does not call the API when a blank search is submitted', async () => {
      await searchFor('   ');

      expect(searchBooks).not.toHaveBeenCalled();
    });
  });

  describe('results', () => {
    // Ranking is the server's job. The client must show results exactly in the
    // order the API returns them and never re-sort them.
    describe('keeps the API order and never re-sorts on the client', () => {
      // Five books whose title, author, year, ID and tier are all different, so
      // sorting by any one of them gives a different order than the others.
      const ranked: BookResult[] = [
        { ...graphicNovel, title: 'Zeta', author: 'Cole', first_publish_year: 1990, open_library_id: 'OL3W', tier: 'C' },
        { ...graphicNovel, title: 'Alpha', author: 'Eve', first_publish_year: 2010, open_library_id: 'OL5W', tier: 'E' },
        { ...graphicNovel, title: 'Mid', author: 'Abe', first_publish_year: 1850, open_library_id: 'OL1W', tier: 'B' },
        { ...graphicNovel, title: 'Beta', author: 'Dan', first_publish_year: 2001, open_library_id: 'OL4W', tier: 'A' },
        { ...graphicNovel, title: 'Omega', author: 'Bea', first_publish_year: 1700, open_library_id: 'OL2W', tier: 'D' },
      ];

      const shownIds = () => results().map((li) => li.querySelector('.book-id code')?.textContent);
      const idsOf = (books: BookResult[]) => books.map((book) => book.open_library_id);

      function permutations<T>(items: T[]): T[][] {
        if (items.length <= 1) {
          return [items];
        }
        return items.flatMap((item, i) =>
          permutations([...items.slice(0, i), ...items.slice(i + 1)]).map((rest) => [item, ...rest]),
        );
      }

      it('shows results as a numbered list starting at 1', async () => {
        searchBooks.mockResolvedValue(response(ranked));

        await searchFor('anything');

        const list = page().querySelector('ol.results')!;
        expect(list.hasAttribute('reversed')).toBe(false);
        expect(list.hasAttribute('start')).toBe(false);
        expect(list.children).toHaveLength(ranked.length);
      });

      it('keeps an order that matches no sort by title, author, year, ID or tier', async () => {
        searchBooks.mockResolvedValue(response(ranked));

        await searchFor('anything');

        expect(shownIds()).toEqual(['OL3W', 'OL5W', 'OL1W', 'OL4W', 'OL2W']);
      });

      // Any client-side sort turns an order and its reverse into the same output,
      // so passing both proves the client isn't sorting by any field.
      it('shows an order and its reverse each exactly as given', async () => {
        const reversed = [...ranked].reverse();

        searchBooks.mockResolvedValueOnce(response(ranked));
        await searchFor('anything');
        expect(shownIds()).toEqual(idsOf(ranked));

        searchBooks.mockResolvedValueOnce(response(reversed));
        await clickSearch();
        expect(shownIds()).toEqual(idsOf(reversed));
      });

      // All 120 orders of the five books, searched one after another on the same
      // page, so this also covers Angular reusing list items between searches.
      it('shows every possible order of five results exactly as given', async () => {
        await type('anything');

        for (const order of permutations(ranked)) {
          searchBooks.mockResolvedValueOnce(response(order));
          await clickSearch();

          expect(shownIds()).toEqual(idsOf(order));
          expect(results().map((li) => li.querySelector('h2')?.textContent?.trim())).toEqual(
            order.map((book) => book.title),
          );
        }

        expect(searchBooks).toHaveBeenCalledTimes(120);
      });
    });

    it('shows each book\'s own title, primary author and first publish year', async () => {
      const silmarillion: BookResult = {
        ...graphicNovel,
        title: 'The Silmarillion',
        author: 'J.R.R. Tolkien, Christopher Tolkien',
        first_publish_year: 1977,
        open_library_id: 'OL27513W',
      };
      searchBooks.mockResolvedValue(response([hobbit, silmarillion]));

      await searchFor('tolkien');

      const [first, second] = results();
      expect(first.querySelector('h2')?.textContent?.trim()).toBe('The Hobbit');
      expect(metaItems(first)).toEqual(['J.R.R. Tolkien', '1937']);
      expect(second.querySelector('h2')?.textContent?.trim()).toBe('The Silmarillion');
      expect(metaItems(second)).toEqual(['J.R.R. Tolkien, Christopher Tolkien', '1977']);
    });

    it('shows the Open Library ID with a label', async () => {
      searchBooks.mockResolvedValue(response([hobbit]));

      await searchFor('tolkien hobbit');

      const id = results()[0].querySelector('.book-id');
      expect(id?.textContent).toContain('Open Library ID');
      expect(id?.querySelector('code')?.textContent).toBe('OL27482W');
    });

    it('links the title to the Open Library page in a new tab', async () => {
      searchBooks.mockResolvedValue(response([hobbit]));

      await searchFor('tolkien hobbit');

      const link = results()[0].querySelector('h2 a') as HTMLAnchorElement;
      expect(link.getAttribute('href')).toBe('https://openlibrary.org/works/OL27482W');
      expect(link.target).toBe('_blank');
      expect(link.rel).toContain('noopener');
    });

    it('shows the cover with alt text when one is available', async () => {
      searchBooks.mockResolvedValue(response([hobbit]));

      await searchFor('tolkien hobbit');

      const cover = results()[0].querySelector('img.cover') as HTMLImageElement;
      expect(cover.getAttribute('src')).toBe(hobbit.cover_url);
      expect(cover.alt).toBe('Cover of The Hobbit');
    });

    it('leaves out the cover and the year when Open Library has none', async () => {
      searchBooks.mockResolvedValue(response([graphicNovel]));

      await searchFor('tolkien hobbit');

      const [book] = results();
      expect(book.querySelector('img')).toBeNull();
      expect(metaItems(book)).toEqual(['J.R.R. Tolkien']);
    });

    it('shows the "why this book" explanation for each result', async () => {
      searchBooks.mockResolvedValue(response([hobbit, graphicNovel]));

      await searchFor('tolkien hobbit');

      expect(results().map((li) => li.querySelector('.explanation')?.textContent)).toEqual([
        hobbit.explanation,
        graphicNovel.explanation,
      ]);
    });

    it('shows markup in API text as plain text rather than rendering it', async () => {
      searchBooks.mockResolvedValue(response([{ ...hobbit, explanation: '<b>Exact</b> title match.' }]));

      await searchFor('tolkien hobbit');

      const explanation = results()[0].querySelector('.explanation')!;
      expect(explanation.textContent).toBe('<b>Exact</b> title match.');
      expect(explanation.querySelector('b')).toBeNull();
    });
  });

  describe('no results', () => {
    it('says nothing matched and repeats the search', async () => {
      searchBooks.mockResolvedValue(response([], 'zzzz'));

      await searchFor('zzzz');

      expect(page().querySelector('.empty')?.textContent).toContain('No books matched “zzzz”');
      expect(page().querySelector('.results')).toBeNull();
    });
  });

  describe('slow searches', () => {
    it('shows that a search is running and blocks a second one until it finishes', async () => {
      let finish!: (value: BookSearchResponse) => void;
      searchBooks.mockReturnValue(new Promise<BookSearchResponse>((resolve) => (finish = resolve)));

      await type('tolkien hobbit');
      page().querySelector('form')!.dispatchEvent(new Event('submit'));
      await fixture.whenStable();

      expect(button().textContent?.trim()).toBe('Searching…');
      expect(button().disabled).toBe(true);

      await submit();
      expect(searchBooks).toHaveBeenCalledTimes(1);

      finish(response([hobbit]));
      // Let the component's await and finally run before checking the page.
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();

      expect(button().textContent?.trim()).toBe('Search');
      expect(button().disabled).toBe(false);
      expect(results()).toHaveLength(1);
    });
  });

  describe('errors', () => {
    it('shows the validation message from a 400', async () => {
      searchBooks.mockRejectedValue(
        new HttpErrorResponse({ status: 400, error: { errors: { q: ['Search terms can be at most 200 characters.'] } } }),
      );

      await searchFor('austen');

      expect(error()).toBe('Search terms can be at most 200 characters.');
    });

    it('falls back to a general message when a 400 has no details', async () => {
      searchBooks.mockRejectedValue(new HttpErrorResponse({ status: 400 }));

      await searchFor('austen');

      expect(error()).toBe('Check your search and try again.');
    });

    it('shows Open Library\'s reason when it refuses the search (422)', async () => {
      searchBooks.mockRejectedValue(
        new HttpErrorResponse({
          status: 422,
          error: { title: 'Open Library refused this search.', status: 422, detail: 'Query too short, must be at least 3 characters.' },
        }),
      );

      await searchFor('of');

      expect(error()).toBe("Open Library couldn't run this search: Query too short, must be at least 3 characters.");
      expect(page().querySelector('.error')?.getAttribute('role')).toBe('alert');
    });

    it('asks for different search terms when a 422 has no reason', async () => {
      searchBooks.mockRejectedValue(new HttpErrorResponse({ status: 422 }));

      await searchFor('of');

      expect(error()).toBe("Open Library couldn't run this search. Try different search terms.");
    });

    it('does not call a refused search unavailable', async () => {
      searchBooks.mockRejectedValue(new HttpErrorResponse({ status: 422, error: { detail: 'Not allowed: the.' } }));

      await searchFor('the');

      expect(error()).not.toContain('unavailable');
      expect(button().disabled).toBe(false);
    });

    it('says search is unavailable when Open Library or Gemini is down (502)', async () => {
      searchBooks.mockRejectedValue(new HttpErrorResponse({ status: 502 }));

      await searchFor('austen');

      expect(error()).toContain('unavailable');
      expect(page().querySelector('.error')?.getAttribute('role')).toBe('alert');
    });

    it('says search is unavailable when the API cannot be reached', async () => {
      searchBooks.mockRejectedValue(new HttpErrorResponse({ status: 0 }));

      await searchFor('austen');

      expect(error()).toContain('unavailable');
    });

    it('clears earlier results when a search fails', async () => {
      searchBooks.mockResolvedValueOnce(response([hobbit]));
      await searchFor('tolkien hobbit');
      expect(results()).toHaveLength(1);

      searchBooks.mockRejectedValueOnce(new HttpErrorResponse({ status: 502 }));
      await submit();

      expect(results()).toHaveLength(0);
      expect(error()).toContain('unavailable');
    });

    it('clears the error when the next search succeeds', async () => {
      searchBooks.mockRejectedValueOnce(new HttpErrorResponse({ status: 502 }));
      await searchFor('tolkien hobbit');
      expect(error()).toBeTruthy();

      searchBooks.mockResolvedValueOnce(response([hobbit]));
      await submit();

      expect(error()).toBeUndefined();
      expect(results()).toHaveLength(1);
    });
  });
});
