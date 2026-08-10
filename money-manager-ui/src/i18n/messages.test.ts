/**
 * The two locale files must describe exactly the same set of keys.
 *
 * This is the test that earns its keep in this whole change. The realistic failure is not a bad
 * translation — someone will notice a clumsy Hungarian sentence. It is a feature added later in
 * English only: the developer sees correct text, every other check passes, and the customer sees
 * a raw `dashboard.totalRent` key in production. Nothing else in the suite can catch that,
 * because a missing key is not a type error and not a lint error.
 *
 * Both directions are checked. A key added only to Hungarian is the same defect wearing the
 * other hat — it renders as English through the fallback, which looks deliberate and is not.
 */
import { describe, it, expect } from 'vitest';
import en from '../locales/en.json';
import hu from '../locales/hu.json';

type Messages = { [key: string]: string | Messages };

/** Every leaf path in the tree, as `a.b.c`, so nesting differences show as a missing key. */
function leafKeys(messages: Messages, prefix = ''): string[] {
  return Object.entries(messages).flatMap(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    return typeof value === 'string' ? [path] : leafKeys(value, path);
  });
}

const enKeys = leafKeys(en as Messages).sort();
const huKeys = leafKeys(hu as Messages).sort();

describe('locale files', () => {
  it('has no key that Hungarian is missing', () => {
    // The damaging direction: a key only in English renders as English for a Hungarian customer.
    expect(enKeys.filter((key) => !huKeys.includes(key))).toEqual([]);
  });

  it('has no key that English is missing', () => {
    expect(huKeys.filter((key) => !enKeys.includes(key))).toEqual([]);
  });

  it('has no empty translations', () => {
    // An empty string satisfies the key check above and renders as nothing at all — a blank
    // button, a heading that vanished. Worse than a missing key, which at least looks wrong.
    const blanks = (messages: Messages, prefix = ''): string[] =>
      Object.entries(messages).flatMap(([key, value]) => {
        const path = prefix ? `${prefix}.${key}` : key;
        if (typeof value !== 'string') return blanks(value, path);
        return value.trim().length === 0 ? [path] : [];
      });

    expect(blanks(hu as Messages)).toEqual([]);
    expect(blanks(en as Messages)).toEqual([]);
  });

  it('actually contains Hungarian', () => {
    // A guard against the copy-paste start: `hu.json` seeded from `en.json` and never
    // translated passes every check above while shipping an English app under a Hungarian flag.
    // Comparing the two files directly is the only thing that notices.
    const identical = enKeys.filter((key) => {
      const read = (m: Messages) =>
        key.split('.').reduce<string | Messages | undefined>(
          (node, part) => (typeof node === 'object' && node ? node[part] : undefined),
          m
        );

      return read(en as Messages) === read(hu as Messages);
    });

    // Some entries are legitimately the same in both — proper nouns and language names, which
    // are conventionally written in their own language whatever the surrounding text.
    expect(identical).toEqual([
      'app.name',
      'settings.languageName.en',
      'settings.languageName.hu',
    ]);
  });
});
