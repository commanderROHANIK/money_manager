import { expect, test } from '@playwright/test';
import { signIn } from './helpers';

/**
 * Issue #63: the two add-forms this deployment shipped with nothing behind. The backend and the
 * service layer already existed — `BankAccounts.vue`'s modal was a literal empty placeholder and
 * `StockPage.vue` had no add-holding UI at all — so the only real proof these forms work is
 * exercising the full path through a browser, which is what this spec is for.
 *
 * <p>Runs against the seeded container with Banking and Stocks switched on (see CLAUDE.md's e2e
 * section) — the demo seed itself stays rental-only, but the router redirects away from
 * `/accounts` and `/stocks` while their flags are off, so reaching either form needs them
 * reachable for the length of this run.</p>
 *
 * <p>Each section is add-then-delete, not just add: a form that can create a row but never lets
 * it be removed again fails the moment someone adds a test entry to try it out, and
 * `HoldingsListWidget` had no delete affordance at all before this issue.</p>
 */

test.describe('bank accounts', () => {
  test('adds an account, shows it in its own currency, and removes it again', async ({ page }) => {
    await signIn(page);
    await page.goto('/accounts');

    await page.getByRole('button', { name: '+ Add Account', exact: true }).click();

    await page.getByPlaceholder('Account name', { exact: true }).fill('E2E checking');
    await page.getByPlaceholder('Bank name', { exact: true }).fill('E2E Bank');
    await page.getByPlaceholder('Account number', { exact: true }).fill('E2E-001');
    await page.getByPlaceholder('Account type', { exact: true }).fill('Checking');
    await page.getByPlaceholder('Balance', { exact: true }).fill('1234');
    await page.getByRole('combobox').selectOption('USD');

    await page.getByRole('button', { name: 'Add account', exact: true }).click();

    // The modal closes on success, and the new row shows up formatted in the currency it was
    // entered in — the defect this issue's quality-debt pass fixed alongside the missing form.
    await expect(page.getByText('Add a bank account', { exact: true })).toHaveCount(0);
    await expect(page.getByText('E2E checking - E2E Bank', { exact: true })).toBeVisible();
    await expect(page.getByText('$1,234', { exact: false })).toBeVisible();

    await page.getByRole('button', { name: 'Remove E2E checking' }).click();

    await expect(page.getByText('E2E checking - E2E Bank', { exact: true })).toHaveCount(0);
  });
});

test.describe('stock holdings', () => {
  test('adds a holding, shows it in its own currency, and removes it again', async ({ page }) => {
    await signIn(page);
    await page.goto('/stocks');

    await page.getByPlaceholder('Ticker', { exact: true }).fill('E2E');
    await page.getByPlaceholder('Shares owned', { exact: true }).fill('5');
    await page.getByPlaceholder('Purchase price', { exact: true }).fill('100');
    await page.getByPlaceholder('Current price', { exact: true }).fill('120');
    await page.locator('input[type="date"]').fill('2026-01-15');
    await page.getByRole('combobox').selectOption('GBP');

    await page.getByRole('button', { name: 'Add holding', exact: true }).click();

    await expect(page.getByText('E2E', { exact: true })).toBeVisible();
    await expect(page.getByText('£120', { exact: false })).toBeVisible();

    await page.getByRole('button', { name: 'Remove E2E' }).click();

    await expect(page.getByText('5 shares', { exact: true })).toHaveCount(0);
  });
});
