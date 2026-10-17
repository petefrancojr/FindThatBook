import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BookSearchResponse } from '../../api/generated';
import { appConfig } from '../app.config';
import { Search } from './search';

// Uses the app's real providers and the real generated BookSearchService, with
// Angular's test backend in place of the network. This checks the request the
// browser would actually send, including the root URL set in app.config.ts.
describe('Search (HTTP)', () => {
  let fixture: ComponentFixture<Search>;
  let http: HttpTestingController;

  const response: BookSearchResponse = {
    query: 'austen bennet',
    results: [
      {
        title: 'Pride and Prejudice',
        author: 'Jane Austen',
        first_publish_year: 1813,
        open_library_id: 'OL66554W',
        url: 'https://openlibrary.org/works/OL66554W',
        tier: 'A',
        explanation: 'Austen is the primary author; "Bennet" is the family at the centre of the novel.',
      },
    ],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Search],
      providers: [...appConfig.providers, provideHttpClientTesting()],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Search);
    await fixture.whenStable();
  });

  afterEach(() => {
    http.verify();
  });

  const page = () => fixture.nativeElement as HTMLElement;

  async function searchFor(text: string) {
    const input = page().querySelector('input') as HTMLInputElement;
    input.value = text;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    (page().querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();
  }

  // Lets the component's await and finally run after a response is flushed.
  async function settle() {
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  it('sends GET /api/books/search on the same origin with the query encoded', async () => {
    await searchFor('  austen bennet  ');

    const req = http.expectOne((r) => r.url === '/api/books/search');
    expect(req.request.method).toBe('GET');
    expect(req.request.urlWithParams).toBe('/api/books/search?q=austen%20bennet');
    expect(req.request.headers.get('Accept')).toBe('application/json');

    req.flush(response);
    await settle();

    expect(page().querySelector('.results h2')?.textContent?.trim()).toBe('Pride and Prejudice');
  });

  it('encodes characters that have a meaning in URLs', async () => {
    await searchFor('Brontë & "jane eyre" #1 +more');

    const req = http.expectOne((r) => r.url === '/api/books/search');
    expect(req.request.urlWithParams).toBe(
      '/api/books/search?q=' + encodeURIComponent('Brontë & "jane eyre" #1 +more'),
    );

    req.flush({ ...response, results: [] });
    await settle();
  });

  it('shows the API\'s validation message from a real 400 response', async () => {
    await searchFor('austen');

    http
      .expectOne((r) => r.url === '/api/books/search')
      .flush(
        { title: 'One or more validation errors occurred.', status: 400, errors: { q: ['Enter a search term.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle();

    expect(page().querySelector('.error')?.textContent?.trim()).toBe('Enter a search term.');
  });

  it('shows the unavailable message from a real 502 response', async () => {
    await searchFor('austen');

    http
      .expectOne((r) => r.url === '/api/books/search')
      .flush({ title: 'Bad Gateway', status: 502 }, { status: 502, statusText: 'Bad Gateway' });
    await settle();

    expect(page().querySelector('.error')?.textContent).toContain('unavailable');
  });
});
