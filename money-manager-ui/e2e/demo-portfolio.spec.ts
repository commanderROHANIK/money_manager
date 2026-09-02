import { expect, test, type Page } from '@playwright/test';
import { rowsUnder, signIn } from './helpers';

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

/** The rows of the "All Properties" card specifically. */
const propertyRows = (page: Page) => rowsUnder(page, 'All Properties');

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

  test('the let properties are current, or at most the current month behind', async ({ page }) => {
    await signIn(page);
    await page.goto('/properties');

    // Budapest is paid up to and including this month, on purpose, so that one property in the
    // portfolio is unambiguously green and "in arrears" does not read as the app's normal state.
    await expect(propertyRows(page).filter({ hasText: FORINT })).not.toContainText('behind');

    // Utrecht deliberately has only the current month unrecorded, so the rent schedule and the
    // arrears list have something other than green to show. Whether that surfaces as a badge at
    // all depends on the day of the month against the 5th it falls due, which is why this asserts
    // magnitude rather than presence.
    //
    // The regression it guards is the old seed's: rent for 6 months of an 18-month tenancy, which
    // put a *year* of arrears on the property meant to look healthy. The label pluralises above
    // one month, so "months" appearing here is that bug returning, while "1 month" is the design.
    await expect(propertyRows(page).filter({ hasText: HEALTHY })).not.toContainText('months');
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

  test('the total monthly rent figure is a real converted number, not blank or a raw cross-currency sum', async ({
    page,
  }) => {
    // Total Rent used to add HUF and EUR rent amounts together as if the same unit — a real,
    // shipping bug this asserts against directly, on the same two-currency seed the test above
    // covers. Not blank either: the seeded manual rate is what turns "cannot be known" into a
    // real figure, same as the portfolio summary above.
    await signIn(page);
    await page.goto('/properties');

    const tile = page.getByText('Total Monthly Rent', { exact: true }).locator('xpath=..');

    await expect(tile).not.toContainText('—');
    await expect(tile).toContainText(/[€$£]|\bFt\b/);
  });
});
