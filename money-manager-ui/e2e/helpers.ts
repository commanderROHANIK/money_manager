import { expect, type Page } from '@playwright/test';

/**
 * Shared setup for the end-to-end specs.
 *
 * <p>Extracted when the onboarding suite arrived and needed the same sign-in against a second
 * container: two copies of a login helper drift, and the one that drifts is the one nobody is
 * looking at when a selector changes.</p>
 */

export const USERNAME = process.env.E2E_USERNAME ?? 'demo';

export function seededPassword(): string {
  const value = process.env.E2E_PASSWORD;

  if (!value) {
    // Thrown from the helper rather than at module scope so `playwright test --list` still works
    // without credentials — listing the suite is how you check it compiles.
    throw new Error(
      'E2E_PASSWORD is not set. It must match Seed__Password on the instance under test. There is ' +
        'deliberately no default: with registration disabled, the seeded account is the only way in.'
    );
  }

  return value;
}

export async function signIn(page: Page): Promise<void> {
  // Pinned before the first navigation, and it survives login's reload because it lives in
  // localStorage under the same key the language picker writes. Without it the app comes up in
  // Hungarian — DEFAULT_LOCALE — and every English assertion would be testing the wrong language
  // while appearing to test the right thing.
  await page.addInitScript("window.localStorage.setItem('locale', 'en')");

  await page.goto('/login');
  await page.locator('input[autocomplete="username"]').fill(USERNAME);
  await page.locator('input[autocomplete="current-password"]').fill(seededPassword());
  await page.locator('button[type="submit"]').click();

  // Login pushes to '/' and then calls window.location.reload(). Waiting on the URL alone races
  // that reload — the assertion can pass against a document that is about to be torn down — so
  // each spec re-navigates explicitly afterwards, which makes the torn-down document moot.
  await page.waitForURL((url) => !url.pathname.startsWith('/login'));
}

/**
 * The rows of a card identified by its heading. Scoped by hopping from the heading to its widget
 * root, because names and labels also appear in the widgets above, and an unscoped role query
 * would match several rows and fail strict mode for a reason unrelated to the assertion.
 */
export function rowsUnder(page: Page, heading: string) {
  return page
    .getByRole('heading', { name: heading, exact: true })
    .locator('xpath=..')
    .getByRole('listitem');
}

/** Waits for the dashboard to have finished its widget fetches before asserting on absence. */
export async function openDashboard(page: Page): Promise<void> {
  await page.goto('/');

  // An absence assertion against a page that has not finished loading passes for the wrong
  // reason. The rental property summary is not feature-gated and is served by the same
  // authenticated session the checklist would be, so once it is on screen the onboarding request
  // has had its chance too.
  await expect(page.getByText('Rental Properties', { exact: true })).toBeVisible();
}
