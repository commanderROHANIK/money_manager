import { expect, test, type Page } from '@playwright/test';

/**
 * What the demo seed promises, asserted through a browser against a running deployment image.
 *
 * <p>`DemoDataSeederTests` already proves the rows land in the database. That is a different
 * claim from the one that matters here: that somebody handed the URL can log in and *see* the
 * three things this product is sold on — a portfolio that spans two currencies and still totals,
 * a property that is visibly underperforming, and a figure that admits which of its inputs is
 * missing. Every one of those crosses the API, the analytics calculator, the currency converter,
 * the SPA fallback and four locale files before it reaches a reader, and none of the existing
 * suites cross more than one of them.</p>
 *
 * <p>The specs deliberately assert the contrast rather than the presence. "Kerkstraat 8 warns
 * that it has no valuation" passes on a seed where nothing has a valuation, which is a worse
 * demo than the one this replaced. Paired against "Maple Court does not warn", it only passes on
 * a seed that has both.</p>
 */

const USERNAME = process.env.E2E_USERNAME ?? 'demo';

/**
 * Seeded property names are data rather than translated copy, so they read identically in all
 * four locales. They are the only strings below that do; everything else is pinned to English by
 * {@link signIn}.
 */
const HEALTHY = 'Maple Court, Flat 2';
const FORINT = 'Rákóczi út 12, Flat 4';
const VACANT = 'Kerkstraat 8';

/**
 * Built server-side by PropertyAnalyticsCalculator and handed to the UI as data, which is why it
 * is asserted verbatim in English while the rest of the page is localized. The exact sentence is
 * the product's promise that a soft number says so — rewording it should have to fail a test.
 */
const NO_VALUATION =
  'No valuation recorded, so the purchase price is used as the current value and appreciation reads as zero.';

function seededPassword(): string {
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

async function signIn(page: Page): Promise<void> {
  // Pinned before the first navigation, and it survives login's reload because it lives in
  // localStorage under the same key the language picker writes. Without it the app comes up in
  // Hungarian — DEFAULT_LOCALE — and every English assertion below would be testing the wrong
  // language while appearing to test the right thing.
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
 * The rows of the "All Properties" card specifically. Property names also appear in the widgets
 * above it, so an unscoped `getByRole('listitem')` would match several rows and fail strict mode
 * for a reason that has nothing to do with the assertion.
 */
function propertyRows(page: Page) {
  return page
    .getByRole('heading', { name: 'All Properties', exact: true })
    .locator('xpath=..')
    .getByRole('listitem');
}

async function openProperty(page: Page, name: string): Promise<void> {
  await page.goto('/properties');
  await propertyRows(page).filter({ hasText: name }).getByRole('link', { name }).click();
  await expect(page.getByRole('heading', { name, level: 1 })).toBeVisible();
}

test.describe('the seeded demo portfolio', () => {
  test('the seeded account is a working way in', async ({ page }) => {
    await signIn(page);

    // Registration is disabled on a deployment, so this is not a login test — it is the test that
    // the environment is reachable at all. A seed that creates a portfolio under an account whose
    // password does not work is indistinguishable from an empty deployment.
    await expect(page).not.toHaveURL(/\/login/);
  });

  test('all three demo properties are listed', async ({ page }) => {
    await signIn(page);
    await page.goto('/properties');

    for (const name of [HEALTHY, FORINT, VACANT]) {
      await expect(propertyRows(page).filter({ hasText: name })).toHaveCount(1);
    }
  });

  test('the vacancy reads as vacant and the let properties read as let', async ({ page }) => {
    await signIn(page);
    await page.goto('/properties');

    // Occupancy is derived from the tenancy running today rather than stored, so this also checks
    // that the seeded lease dates actually straddle now — a seed written with fixed dates would
    // drift into being wrong months later and this is what would catch it.
    await expect(propertyRows(page).filter({ hasText: VACANT })).toContainText('Vacant');
    await expect(propertyRows(page).filter({ hasText: HEALTHY })).toContainText('Rented');
    await expect(propertyRows(page).filter({ hasText: FORINT })).toContainText('Rented');
  });

  test('the let properties are not shown as in arrears', async ({ page }) => {
    await signIn(page);
    await page.goto('/properties');

    // The seed this replaced recorded rent for 6 months of an 18-month tenancy, so the property
    // meant to look healthy carried a year of arrears in the one view a demo opens on. The rent
    // schedule derives that from the ledger on every request, so nothing but a browser check
    // would have noticed.
    await expect(propertyRows(page).filter({ hasText: HEALTHY })).not.toContainText('behind');
    await expect(propertyRows(page).filter({ hasText: FORINT })).not.toContainText('behind');
  });

  test('the property with no valuation says so', async ({ page }) => {
    await signIn(page);
    await openProperty(page, VACANT);

    await expect(page.getByText(NO_VALUATION)).toBeVisible();
  });

  test('the property with a valuation carries no such warning', async ({ page }) => {
    await signIn(page);
    await openProperty(page, HEALTHY);

    // The half of the pair that makes the other half mean something: this is what distinguishes
    // "the seed exercises the unknown-input path" from "the seed forgot to record any valuations".
    await expect(page.getByText(NO_VALUATION)).toHaveCount(0);
  });

  test('the two-currency portfolio totals, and discloses the rate it used', async ({ page }) => {
    await signIn(page);
    await page.goto('/properties');

    const portfolio = page
      .getByRole('heading', { name: 'Portfolio', exact: true })
      .locator('xpath=..');

    // Refusing to add unlike currencies is correct behaviour, but a demo that opens on it shows
    // the guardrail instead of the product. The seeded manual EUR/HUF rate is what turns the
    // refusal into a total, so its absence should fail here rather than merely look disappointing.
    await expect(portfolio).not.toContainText('No exchange rate on record');
    await expect(portfolio).toContainText('Converted to');

    // And the disclosure names the provenance rather than just the arithmetic. "rate you entered"
    // is the manual wording; if this ever reads as the ECB one, something has overwritten a rate
    // the user set, which is an invariant violation and not a cosmetic difference.
    await expect(portfolio).toContainText('rate you entered');
  });
});
