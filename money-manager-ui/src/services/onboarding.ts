import { api } from './api';

/**
 * What this landlord has already done, as the API derives it. Mirrors `OnboardingProgressDto`.
 *
 * <p>Every field answers "does anything of this kind exist", never "has this step been marked
 * finished". That is what lets deleting the only property put its step back — a stored flag
 * would go on insisting the landlord had got started.</p>
 */
export interface OnboardingProgress {
  hasProperty: boolean;
  /**
   * Set only when there is exactly one property — never a guess at which of several. That is
   * what lets the checklist deep-link a portfolio-wide step (tenancy/ledger/valuation) straight
   * to the property it is missing, without picking one out of several candidates.
   */
  soleRentalPropertyId: number | null;
  hasLease: boolean;
  hasTransaction: boolean;
  hasValuation: boolean;
  hasBankAccount: boolean;
  hasLoan: boolean;
  hasStock: boolean;
}

export async function fetchOnboardingProgress(): Promise<OnboardingProgress> {
  const response = await api.get<OnboardingProgress>('/Onboarding');

  return response.data;
}
