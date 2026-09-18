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
 * <p>Each section is add-then-edit-then-delete: a form that can create a row but never lets it
 * be removed again fails the moment someone adds a test entry to try it out, and
 * `HoldingsListWidget` had no delete affordance at all before this issue. Issue #77 added the
 * edit round trip in between — both `PUT` endpoints already existed, but nothing in the UI
 * reached them, so fixing a typo'd balance or ticker meant deleting and re-adding the row.</p>
 *
 * <p>Both sections share a single sign-in rather than each getting its own: `AuthController` is
 * rate-limited at 10 requests per IP per minute (`Program.cs`'s "auth" policy, deliberately —
 * see CLAUDE.md), and this suite already spends 9 of those 10 on `demo-portfolio.spec.ts` and
 * onboarding's "an established portfolio" case against this same seeded container. A second
 * `signIn` here would be the 11th call inside that window and get 429'd, which surfaces as the
 * *next* test hanging at `/login` rather than as a failure here — worth the sharing rather than
 * the two independent logins this would otherwise read most naturally as.</p>
 */

test.describe('bank accounts and stock holdings', () => {
  test('adds an account and a holding, shows each in its own currency, and removes both again', async ({
    page,
  }) => {
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

    // Editing must go through the same PUT the add-form's POST landed next to, and the list
    // must reflect the new balance without a manual refresh — this is the round trip #77 added.
    // Scoped to the dialog: StockPage keeps its own add-form permanently on screen, so an
    // unscoped placeholder lookup during a stock edit below would resolve two elements.
    await page.getByRole('button', { name: 'Edit E2E checking' }).click();
    const editAccountDialog = page.getByRole('dialog', { name: 'Edit bank account' });

    // Issue #86: every field in this dialog is prefilled from the account being edited, so the
    // native placeholder — which disappears the instant a field has a value — was these fields'
    // only explanation and was invisible for the entire time the dialog is open. `label` is the
    // persistent fix; assert it renders rather than assuming, since BaseInput's root `<label>`
    // makes `getByLabel` resolve once the `label` prop is populated.
    await expect(editAccountDialog.getByLabel('Account name', { exact: true })).toBeVisible();
    await expect(editAccountDialog.getByLabel('Balance', { exact: true })).toBeVisible();

    await editAccountDialog.getByPlaceholder('Balance', { exact: true }).fill('4321');
    await editAccountDialog.getByRole('button', { name: 'Save changes', exact: true }).click();

    await expect(page.getByText('Edit bank account', { exact: true })).toHaveCount(0);
    await expect(page.getByText('$4,321', { exact: false })).toBeVisible();
    await expect(page.getByText('$1,234', { exact: false })).toHaveCount(0);

    await page.getByRole('button', { name: 'Remove E2E checking' }).click();

    await expect(page.getByText('E2E checking - E2E Bank', { exact: true })).toHaveCount(0);

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

    // Same round trip as the bank account above, through HoldingsListWidget's own PUT. Scoped
    // to the dialog: AddStockWidget's own "Current price" input is still on screen behind the
    // modal, so an unscoped lookup resolves two elements.
    await page.getByRole('button', { name: 'Edit E2E', exact: true }).click();
    const editStockDialog = page.getByRole('dialog', { name: 'Edit holding' });

    // Same as the bank account dialog above: EditStockWidget's fields are all prefilled from the
    // holding being edited (issue #86).
    await expect(editStockDialog.getByLabel('Ticker', { exact: true })).toBeVisible();
    await expect(editStockDialog.getByLabel('Current price', { exact: true })).toBeVisible();

    await editStockDialog.getByPlaceholder('Current price', { exact: true }).fill('150');
    await editStockDialog.getByRole('button', { name: 'Save changes', exact: true }).click();

    await expect(page.getByText('Edit holding', { exact: true })).toHaveCount(0);
    await expect(page.getByText('£150', { exact: false })).toBeVisible();
    await expect(page.getByText('£120', { exact: false })).toHaveCount(0);

    await page.getByRole('button', { name: 'Remove E2E' }).click();

    await expect(page.getByText('5 shares', { exact: true })).toHaveCount(0);
  });
});
