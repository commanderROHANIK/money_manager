/**
 * The language picker, which is the only way a user reaches the English build.
 *
 * The assertions worth having are that picking a language changes the whole application rather
 * than only the words — the formatters read a different piece of state from `t()` — and that the
 * choice survives a reload, since a setting that forgets itself is worse than none.
 */
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mount } from '@vue/test-utils';
import { setLocale } from '../../../i18n';
import {
  currentLocale,
  DEFAULT_LOCALE,
  LOCALE_STORAGE_KEY,
  SUPPORTED_LOCALES,
} from '../../../i18n/locale';
import { formatMoney } from '../../../utils/money';
import LanguageSettingsWidget from './LanguageSettingsWidget.vue';

beforeEach(() => {
  localStorage.clear();
  setLocale(DEFAULT_LOCALE);
});

afterEach(() => {
  localStorage.clear();
  setLocale(DEFAULT_LOCALE);
});

describe('LanguageSettingsWidget', () => {
  it('offers every supported language, each written in its own language', () => {
    const options = mount(LanguageSettingsWidget).findAll('option');

    // Someone who switched to a language they cannot read has to be able to find their way back,
    // which they cannot do if the list is written in the language they are trying to leave.
    expect(options.map((o) => o.text())).toEqual(['Magyar', 'English', 'Deutsch', 'Français']);
  });

  it('offers exactly the locales the application ships', () => {
    const values = mount(LanguageSettingsWidget)
      .findAll('option')
      .map((o) => o.element.value);

    // Pinned against SUPPORTED_LOCALES rather than a literal list, so adding a language is one
    // edit rather than two — and a language added to the picker without messages behind it fails
    // in messages.test.ts instead of rendering the whole app as raw keys.
    expect(values).toEqual([...SUPPORTED_LOCALES]);
  });

  it('starts on the language currently in use', () => {
    const wrapper = mount(LanguageSettingsWidget);

    expect(wrapper.find('select').element.value).toBe('hu');
  });

  it('switches the whole application, not only the words', async () => {
    const wrapper = mount(LanguageSettingsWidget);

    await wrapper.find('select').setValue('en');

    expect(currentLocale.value).toBe('en');

    // The part a translation-only change would miss. formatMoney reads the locale rather than
    // t(), so if the two were separate pieces of state this would still be grouping in the
    // Hungarian style after the labels had switched.
    expect(formatMoney(1234567, 'HUF')).toContain('1,234,567');
  });

  it('remembers the choice for the next visit', async () => {
    const wrapper = mount(LanguageSettingsWidget);

    await wrapper.find('select').setValue('en');

    expect(localStorage.getItem(LOCALE_STORAGE_KEY)).toBe('en');
  });

  it('marks the document so a screen reader announces the right language', async () => {
    const wrapper = mount(LanguageSettingsWidget);

    await wrapper.find('select').setValue('en');
    expect(document.documentElement.getAttribute('lang')).toBe('en');

    await wrapper.find('select').setValue('hu');
    expect(document.documentElement.getAttribute('lang')).toBe('hu');
  });
});
