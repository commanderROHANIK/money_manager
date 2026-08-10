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
 * Switches language everywhere at once: the translated strings, the number and date formatting
 * (through `currentLocale`, which the formatters read), the `<html lang>` a screen reader
 * announces, and the stored preference for the next visit.
 *
 * <p>One function rather than four call sites, because the failure mode of doing it piecemeal is
 * a half-translated page — Hungarian labels next to English dates — which reads as a bug in the
 * data rather than a missed call.</p>
 */
export function setLocale(locale: Locale): void {
  currentLocale.value = locale;
  i18n.global.locale.value = locale;
  storeLocale(locale);

  if (typeof document !== 'undefined') {
    document.documentElement.setAttribute('lang', locale);
  }
}

/** Applies whatever the last visit chose. Called once, before the app mounts. */
export function initLocale(): void {
  setLocale(readStoredLocale());
}
