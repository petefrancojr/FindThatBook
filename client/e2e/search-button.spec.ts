import { expect, Page, test } from '@playwright/test';

// The search button, tested in a real browser against the built site. The API is answered in the browser with
// page.route, so these tests check the page alone and never call Gemini or Open Library.

const hobbit = {
  query: 'tolkien hobbit',
  results: [
    {
      title: 'The Hobbit',
      author: 'J.R.R. Tolkien',
      first_publish_year: 1937,
      open_library_id: 'OL27482W',
      url: 'https://openlibrary.org/works/OL27482W',
      cover_url: null,
      tier: 'A',
      explanation: 'Exact title; J.R.R. Tolkien is primary author.',
    },
  ],
};

/** Answers every search with the given status and body, after an optional delay, and records each request's URL. */
async function answerSearches(page: Page, body: unknown, status = 200, delayMs = 0): Promise<URL[]> {
  const requests: URL[] = [];
  await page.route('**/api/books/search**', async (route) => {
    requests.push(new URL(route.request().url()));
    if (delayMs > 0) {
      await new Promise((resolve) => setTimeout(resolve, delayMs));
    }
    await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });
  });
  return requests;
}

/** How long the finished page stays on screen after each test when a window is shown, so its end state can be seen. */
const endPauseMs = Number(process.env['END_PAUSE_MS'] ?? (process.env['HEADLESS'] === 'true' ? 0 : 2000));

test.describe('search button', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test.afterEach(async ({ page }) => {
    if (endPauseMs > 0) {
      await page.waitForTimeout(endPauseMs);
    }
  });

  test('is disabled until something other than spaces is typed', async ({ page }) => {
    const button = page.getByRole('button', { name: 'Search' });
    const field = page.getByRole('searchbox', { name: 'Title, author or keywords' });

    await expect(button).toBeDisabled();

    await field.fill('   ');
    await expect(button).toBeDisabled();

    await field.fill('tolkien hobbit');
    await expect(button).toBeEnabled();

    await field.fill('');
    await expect(button).toBeDisabled();
  });

  test('searches for what was typed, trimmed, and shows the results', async ({ page }) => {
    const requests = await answerSearches(page, hobbit);

    await page.getByRole('searchbox').fill('  tolkien hobbit  ');
    await page.getByRole('button', { name: 'Search' }).click();

    await expect(page.getByRole('link', { name: 'The Hobbit' })).toBeVisible();
    await expect(page.getByText('Exact title; J.R.R. Tolkien is primary author.')).toBeVisible();
    expect(requests).toHaveLength(1);
    expect(requests[0].searchParams.get('q')).toBe('tolkien hobbit');
  });

  test('also searches when Enter is pressed in the field', async ({ page }) => {
    const requests = await answerSearches(page, hobbit);

    await page.getByRole('searchbox').fill('tolkien hobbit');
    await page.getByRole('searchbox').press('Enter');

    await expect(page.getByRole('link', { name: 'The Hobbit' })).toBeVisible();
    expect(requests).toHaveLength(1);
  });

  test('shows that a search is running and blocks a second one until it finishes', async ({ page }) => {
    const requests = await answerSearches(page, hobbit, 200, 1000);

    await page.getByRole('searchbox').fill('tolkien hobbit');
    await page.getByRole('button', { name: 'Search' }).click();

    const running = page.getByRole('button', { name: 'Searching…' });
    await expect(running).toBeVisible();
    await expect(running).toBeDisabled();

    await expect(page.getByRole('link', { name: 'The Hobbit' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Search' })).toBeEnabled();
    expect(requests).toHaveLength(1);
  });

  test('shows a message when nothing matched', async ({ page }) => {
    await answerSearches(page, { query: 'zzzz', results: [] });

    await page.getByRole('searchbox').fill('zzzz');
    await page.getByRole('button', { name: 'Search' }).click();

    await expect(page.getByText('No books matched “zzzz”.')).toBeVisible();
  });

  test('shows Open Library\'s reason when it refuses the search', async ({ page }) => {
    await answerSearches(
      page,
      { title: 'Open Library refused this search.', status: 422, detail: 'Query too short, must be at least 3 characters.' },
      422,
    );

    await page.getByRole('searchbox').fill('of');
    await page.getByRole('button', { name: 'Search' }).click();

    await expect(page.getByRole('alert')).toHaveText(
      "Open Library couldn't run this search: Query too short, must be at least 3 characters.",
    );
    await expect(page.getByRole('button', { name: 'Search' })).toBeEnabled();
  });

  test('shows an error when search is unavailable, and searches again on the next click', async ({ page }) => {
    await answerSearches(page, { status: 502, title: 'A service this search depends on is unavailable.' }, 502);

    await page.getByRole('searchbox').fill('tolkien hobbit');
    await page.getByRole('button', { name: 'Search' }).click();

    await expect(page.getByRole('alert')).toHaveText('Search is unavailable right now. Try again in a moment.');
    await expect(page.getByRole('button', { name: 'Search' })).toBeEnabled();

    await page.unrouteAll();
    await answerSearches(page, hobbit);
    await page.getByRole('button', { name: 'Search' }).click();

    await expect(page.getByRole('link', { name: 'The Hobbit' })).toBeVisible();
    await expect(page.getByRole('alert')).toHaveCount(0);
  });
});
