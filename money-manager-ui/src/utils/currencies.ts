/**
 * Currencies offered in the entry forms. Deliberately a short list rather than every ISO
 * code — a landlord picks from the handful their portfolio actually uses.
 */
export const CURRENCIES = ['EUR', 'HUF', 'USD', 'GBP', 'CHF', 'PLN', 'CZK', 'RON'] as const;

export type CurrencyCode = (typeof CURRENCIES)[number];
