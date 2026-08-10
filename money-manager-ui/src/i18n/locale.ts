import { ref } from 'vue';

/**
 * The locales this application ships. Hungarian is first because it is the default, not because
 * the list is alphabetical.
 */
export const SUPPORTED_LOCALES = ['hu', 'en'] as const;

export type Locale = (typeof SUPPORTED_LOCALES)[number];

/**
 * Hungarian, unconditionally — not negotiated from `navigator.language`.
 *
 * <p>The customer this is built for is Hungarian, and a browser reporting `en-US` because that is
 * how the machine was set up should not decide what language the product speaks. Anyone who wants
 * English can pick it, and that choice is remembered.</p>
 */
export const DEFAULT_LOCALE: Locale = 'hu';

export const LOCALE_STORAGE_KEY = 'locale';

/**
 * The BCP 47 tag each locale formats numbers and dates with.
 *
 * <p>`en-GB` rather than `en-US`: this is a European product, and `en-US` would render
 * 2026. 08. 05. as 8/5/2026 — a date the rest of the interface disagrees with, in the one place
 * a reader is least likely to notice the difference.</p>
 */
const INTL_LOCALES: Record<Locale, string> = {
  hu: 'hu-HU',
  en: 'en-GB',
};

/**
 * The active locale. A ref rather than a plain variable so that the formatters, which are called
 * from computeds all over the tree, re-run when it changes — `t()` alone would re-render the
 * translated strings and leave every number and date in the previous language.
 */
export const currentLocale = ref<Locale>(DEFAULT_LOCALE);

export function isSupported(value: string | null | undefined): value is Locale {
  return SUPPORTED_LOCALES.includes(value as Locale);
}

/**
 * What the last visit chose, or the default. Reading storage is wrapped because a browser with
 * storage disabled should get a Hungarian app rather than a blank screen.
 */
export function readStoredLocale(): Locale {
  try {
    const stored = localStorage.getItem(LOCALE_STORAGE_KEY);
    return isSupported(stored) ? stored : DEFAULT_LOCALE;
  } catch {
    return DEFAULT_LOCALE;
  }
}

export function storeLocale(locale: Locale): void {
  try {
    localStorage.setItem(LOCALE_STORAGE_KEY, locale);
  } catch {
    // A session that cannot persist the choice should still honour it for this session.
  }
}

/** The tag to hand `Intl`. Read through `currentLocale` so callers stay reactive. */
export function intlLocale(): string {
  return INTL_LOCALES[currentLocale.value];
}
