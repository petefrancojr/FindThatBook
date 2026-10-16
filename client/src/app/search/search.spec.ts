import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BookSearchResponse, BookSearchService } from '../../api/generated';
import { Search } from './search';

describe('Search', () => {
  let component: Search;
  let fixture: ComponentFixture<Search>;
  let searchBooks: ReturnType<typeof vi.fn>;

  const response: BookSearchResponse = {
    query: 'austen bennet',
    interpretedAs: { title: 'Pride and Prejudice', author: 'Jane Austen' },
    results: [
      {
        title: 'Pride and Prejudice',
        author: 'Jane Austen',
        first_publish_year: 1813,
        open_library_id: 'OL66554W',
        url: 'https://openlibrary.org/works/OL66554W',
        tier: 'A',
        explanation: 'Austen is the primary author.',
      },
    ],
  };

  beforeEach(async () => {
    searchBooks = vi.fn();

    await TestBed.configureTestingModule({
      imports: [Search],
      providers: [{ provide: BookSearchService, useValue: { searchBooks } }],
    }).compileComponents();

    fixture = TestBed.createComponent(Search);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  const page = () => fixture.nativeElement as HTMLElement;
  const input = () => page().querySelector('input') as HTMLInputElement;
  const button = () => page().querySelector('button') as HTMLButtonElement;

  async function type(text: string) {
    input().value = text;
    input().dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function submit() {
    page().querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should show the Search heading', () => {
    expect(page().querySelector('h1')?.textContent).toBe('Search');
  });

  it('disables the button until something is typed', async () => {
    expect(button().disabled).toBe(true);

    await type('   ');
    expect(button().disabled).toBe(true);

    await type('austen');
    expect(button().disabled).toBe(false);
  });

  it('calls the search API with the trimmed query and shows the results', async () => {
    searchBooks.mockResolvedValue(response);

    await type('  austen bennet  ');
    await submit();

    expect(searchBooks).toHaveBeenCalledWith({ q: 'austen bennet' });
    expect(page().querySelector('.result h2')?.textContent).toContain('Pride and Prejudice');
    expect(page().querySelector('.meta')?.textContent).toContain('1813');
  });

  it('says so when nothing matched', async () => {
    searchBooks.mockResolvedValue({ ...response, results: [] });

    await type('zzzz');
    await submit();

    expect(page().querySelector('.empty')?.textContent).toContain('No books matched');
  });

  it('shows the validation message from a 400', async () => {
    searchBooks.mockRejectedValue(
      new HttpErrorResponse({ status: 400, error: { errors: { q: ['Search terms can be at most 200 characters.'] } } }),
    );

    await type('austen');
    await submit();

    expect(page().querySelector('.error')?.textContent).toContain('at most 200 characters');
  });

  it('shows a friendly message when the API is unavailable', async () => {
    searchBooks.mockRejectedValue(new HttpErrorResponse({ status: 502 }));

    await type('austen');
    await submit();

    expect(page().querySelector('.error')?.textContent).toContain('unavailable');
  });
});
