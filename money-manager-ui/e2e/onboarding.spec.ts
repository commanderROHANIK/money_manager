import { expect, test } from '@playwright/test';
import { openDashboard, signIn } from './helpers';

/**
 * The onboarding checklist, against two containers rather than one.
 *
 * <p>The claim is a contrast — "a brand-new account sees the checklist, an established one sees
 * no checklist at all" — and a single deployment can only ever demonstrate one half of it. So CI
 * starts a second image with `Seed__IncludeDemoData=false`: the same seeded account, no
 * portfolio. Asserting only the empty half would pass just as well on a checklist that is always
 * shown, which is the defect a reader would actually notice.</p>
 *
 * <p>Both containers ship the MVP feature shape — banking and stocks off, loans on — so the
 * gating is observable here too, and is asserted rather than assumed.</p>
 */

const EMPTY_BASE_URL = process.env.E2E_EMPTY_BASE_URL ?? 'http://localhost:8081';

const CHECKLIST = 'Getting started';

const CORE_STEPS = [
  'Add your first property',
  'Record who is renting it',
  'Enter what you have received and paid',
  'Say what it is worth today',
];

test.describe('an account with nothing in it', () => {
  test.use({ baseURL: EMPTY_BASE_URL });

  test('is shown what to do', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);

    await expect(page.getByRole('heading', { name: CHECKLIST, exact: true })).toBeVisible();

    for (const step of CORE_STEPS) {
      await expect(page.getByText(step, { exact: true })).toBeVisible();
    }
  });

  test('is never told to use a section this deployment switched off', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);

    // The defect the issue names outright: an onboarding checklist telling a landlord to connect
    // a bank account and add stock holdings is describing an app they were not given — those
    // sections are off, their links are absent from the sidebar and their endpoints answer 404.
    await expect(page.getByText('Add a bank account', { exact: true })).toHaveCount(0);
    await expect(page.getByText('Add a holding', { exact: true })).toHaveCount(0);

    // Loans are on in this deployment, so the mortgage step is offered. Paired with the two
    // above so this reads as filtering rather than as the steps simply not existing.
    await expect(page.getByText('Record the mortgage', { exact: true })).toBeVisible();
  });

  test('can be dismissed, and stays dismissed', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);

    await expect(page.getByRole('heading', { name: CHECKLIST, exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Hide', exact: true }).click();

    await expect(page.getByRole('heading', { name: CHECKLIST, exact: true })).toHaveCount(0);

    // Through a reload, because the whole point of putting dismissal in localStorage rather than
    // in component state is that it survives one. Nothing was written to the schema to do it.
    await page.reload();
    await openDashboard(page);

    await expect(page.getByRole('heading', { name: CHECKLIST, exact: true })).toHaveCount(0);
  });
});

test.describe('an established portfolio', () => {
  // The default base URL: the fully seeded demo container the other suite uses.

  test('is not shown a checklist at all', async ({ page }) => {
    await signIn(page);
    await openDashboard(page);

    // The half that makes the other half mean something. This account has properties, tenancies
    // and a ledger, so every required step is already satisfied and the panel unmounts itself —
    // no dismissal needed, and nothing stored to remember that it was finished.
    await expect(page.getByRole('heading', { name: CHECKLIST, exact: true })).toHaveCount(0);
  });
});
