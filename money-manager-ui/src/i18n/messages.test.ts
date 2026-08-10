/**
 * Every locale file must describe exactly the same set of keys.
 *
 * This is the test that earns its keep in the whole localization change. The realistic failure is
 * not a bad translation — someone will notice a clumsy sentence. It is a feature added later in
 * one language only: the developer sees correct text, every other check passes, and a customer
 * sees a raw `dashboard.totalRent` key in production. Nothing else in the suite can catch that,
 * because a missing key is neither a type error nor a lint error.
 *
 * The cost of a language is paid here rather than at the call site, which is the point of
 * checking it this way: adding a fifth locale needs no new test, and adding a key to one file
 * fails until it exists in all of them.
 */
import { describe, it, expect } from 'vitest';
import de from '../locales/de.json';
import en from '../locales/en.json';
import fr from '../locales/fr.json';
import hu from '../locales/hu.json';
import { i18n } from './index';
import { SUPPORTED_LOCALES } from './locale';

type Messages = { [key: string]: string | Messages };

const FILES: Record<string, Messages> = {
  hu: hu as Messages,
  en: en as Messages,
  de: de as Messages,
  fr: fr as Messages,
};

/** Every leaf path in the tree, as `a.b.c`, so a nesting difference shows up as a missing key. */
function leafKeys(messages: Messages, prefix = ''): string[] {
  return Object.entries(messages).flatMap(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    return typeof value === 'string' ? [path] : leafKeys(value, path);
  });
}

function read(messages: Messages, key: string): string | Messages | undefined {
  return key
    .split('.')
    .reduce<string | Messages | undefined>(
      (node, part) => (typeof node === 'object' && node ? node[part] : undefined),
      messages
    );
}

/**
 * English is the reference only because it is the fallback locale. Every file is compared against
 * it in both directions, so which one holds the reference makes no difference to what is caught.
 */
const reference = leafKeys(FILES.en).sort();

describe('locale files', () => {
  it('ships a file for every supported locale', () => {
    // A locale listed in the picker with no messages behind it renders the entire application as
    // raw keys the moment someone selects it.
    expect(Object.keys(FILES).sort()).toEqual([...SUPPORTED_LOCALES].sort());
  });

  it.each(Object.keys(FILES))('%s has exactly the same keys as the reference', (locale) => {
    const keys = leafKeys(FILES[locale]).sort();

    // Both directions. A key only in English renders as English for everyone else; a key only in
    // another file is dead weight that reads as though something is wired up when it is not.
    expect(keys.filter((k) => !reference.includes(k))).toEqual([]);
    expect(reference.filter((k) => !keys.includes(k))).toEqual([]);
  });

  it.each(Object.keys(FILES))('%s has no empty translations', (locale) => {
    // An empty string satisfies the key check above and renders as nothing at all — a blank
    // button, a heading that vanished. Worse than a missing key, which at least looks wrong.
    const blanks = leafKeys(FILES[locale]).filter((key) => {
      const value = read(FILES[locale], key);
      return typeof value === 'string' && value.trim().length === 0;
    });

    expect(blanks).toEqual([]);
  });

  /**
   * Entries that are the same in every language by design: the product name, and the language
   * names, which are written in their own language wherever they appear so that someone who has
   * landed in a language they cannot read can still find their way out.
   */
  const SHARED_EVERYWHERE = [
    'app.name',
    'settings.languageName.de',
    'settings.languageName.en',
    'settings.languageName.fr',
    'settings.languageName.hu',
  ];

  it.each(Object.keys(FILES).filter((l) => l !== 'en'))(
    '%s is actually translated rather than copied from English',
    (locale) => {
      // A guard against the copy-paste start: a file seeded from en.json and never translated
      // passes every check above while shipping English under another language's flag. Comparing
      // the files directly is the only thing that notices.
      const translatable = reference.filter((key) => !SHARED_EVERYWHERE.includes(key));
      const identical = translatable.filter(
        (key) => read(FILES[locale], key) === read(FILES.en, key)
      );

      // A proportion rather than an exact list, because some overlap is real rather than lazy:
      // French genuinely writes Inspection, Note and Vacant the way English does, and pinning an
      // exact set would mean editing this test every time a cognate appeared — which is how a
      // test stops being read and starts being silenced.
      //
      // The defect being caught is wholesale, not marginal: a copied file scores 100%. A fifth of
      // the keys leaves generous room for cognates while staying nowhere near that.
      expect(
        identical.length / translatable.length,
        `${locale} matches English on ${identical.length}/${translatable.length}: ${identical.join(', ')}`
      ).toBeLessThan(0.2);
    }
  );

  it.each(Object.keys(FILES))('%s resolves a pluralised message for every count', (locale) => {
    // Pluralisation is where an i18n layer usually goes wrong quietly. Hungarian needs one form
    // where English needs two, so the count has to select a form per language rather than per
    // message — and if the named value does not arrive, the sentence renders with a literal
    // {count} in it, which no other check would notice.
    const { t } = i18n.global;
    const previous = i18n.global.locale.value;

    i18n.global.locale.value = locale as typeof previous;

    for (const count of [0, 1, 2, 5]) {
      const rendered = t('property.summary.behindOnRent', count);

      expect(rendered, `${locale} @ ${count}`).toContain(String(count));
      expect(rendered, `${locale} @ ${count}`).not.toContain('{');
      expect(rendered, `${locale} @ ${count}`).not.toContain('|');
    }

    i18n.global.locale.value = previous;
  });

  it.each(Object.keys(FILES))('%s keeps every interpolation placeholder', (locale) => {
    // A translator dropping {reason} loses the server's actual error text and leaves a sentence
    // that promises a detail it no longer shows. Renaming it is worse: vue-i18n renders the
    // literal {raison} rather than failing.
    //
    // Which placeholders appear, not how many times. A pluralised message holds its forms in one
    // string separated by |, and languages disagree about how many forms they need — Hungarian
    // does not pluralise a noun after a number, so its single form carries {count} once where
    // English carries it twice across two. Counting occurrences would fail that, correctly
    // written, every time.
    const placeholders = (value: string) => [...new Set(value.match(/\{[^}]+\}/g) ?? [])].sort();

    for (const key of reference) {
      const expected = read(FILES.en, key);
      const actual = read(FILES[locale], key);

      if (typeof expected === 'string' && typeof actual === 'string') {
        expect(placeholders(actual), `${locale}: ${key}`).toEqual(placeholders(expected));
      }
    }
  });
});
