import { watch } from 'vue';
import { createI18n } from 'vue-i18n';
import en from '../locales/en.json';
import hu from '../locales/hu.json';
import { currentLocale, DEFAULT_LOCALE, readStoredLocale, storeLocale } from './locale';
import type { Locale } from './locale';

/**
 * The translation instance.
 *
 * <p>`legacy: false` selects the Composition API mode, which is what makes `t()` usable from
 * `<script setup>` without a global mixin.</p>
 *
 * <p>`fallbackLocale: 'en'` is a safety net, not a design: `messages-are-in-both-files.test.ts`
 * fails the build when a key exists in one locale and not the other, so the fallback should never
 * actually be reached. It is there so that if one ever slips through, the user sees English text
 * rather than a raw `dashboard.title` key.</p>
 */
export const i18n = createI18n({
  legacy: false,
  locale: DEFAULT_LOCALE,
  fallbackLocale: 'en',
  messages: { hu, en },
  // Vue I18n warns for every fallback it performs. The parity test is what catches a missing key,
  // and in a browser the warning would only fire on a defect that test already fails on.
  missingWarn: false,
  fallbackWarn: false,
});

/**
 * `currentLocale` is the single source of truth, and everything else follows it.
 *
 * <p>The obvious alternative — having `setLocale` assign to both `currentLocale` and
 * `i18n.global.locale` — leaves two pieces of state that are only equal because one function
 * happens to write both. Anything that set one directly would produce a page with Hungarian
 * labels and English dates, which reads as a data bug rather than a missed assignment. Driving
 * one from the other makes that state unrepresentable.</p>
 *
 * <p>The dependency only goes this way: `locale.ts` imports nothing from here, so the formatters
 * can read the locale without pulling vue-i18n in behind them.</p>
 */
watch(
  currentLocale,
  (locale) => {
    i18n.global.locale.value = locale;

    if (typeof document !== 'undefined') {
      document.documentElement.setAttribute('lang', locale);
    }
  },
  { immediate: true }
);

/**
 * Switches language everywhere at once: the translated strings, the number and date formatting,
 * the `<html lang>` a screen reader announces, and the stored preference for the next visit.
 */
export function setLocale(locale: Locale): void {
  currentLocale.value = locale;
  storeLocale(locale);
}

/** Applies whatever the last visit chose. Called once, before the app mounts. */
export function initLocale(): void {
  setLocale(readStoredLocale());
}
