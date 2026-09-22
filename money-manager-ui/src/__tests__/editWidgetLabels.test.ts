/**
 * Every `BaseInput`/`BaseSelect` in an `Edit*Widget.vue` must carry a persistent `label`.
 *
 * An edit widget hydrates its whole form from the record being edited before the user ever sees
 * it (`fromAccount`, `fromStock`), so every field is non-empty on first render, always — there is
 * no "briefly empty until typed into" moment the way there is on an add form. `BaseInput`'s
 * `:placeholder` is forwarded straight to the native `<input>` and disappears the instant the
 * field has a value, which for an edit widget means it disappears before the user ever sees it.
 * `label` renders in a `<span>` regardless of value and is the only thing left explaining the
 * field — `EditBankAccountWidget.vue` and `EditStockWidget.vue` both shipped with every field
 * relying on `:placeholder` alone, which is issue #86.
 *
 * This is scoped to `Edit*Widget.vue` specifically because that is the one place "does this field
 * need a label" needs no judgement call — every field there is prefilled by construction, with no
 * exceptions, so there is no false-positive risk. An `Add*Widget.vue` field with a non-empty
 * default (a starting balance of `0`, today's date) has the same problem, but deciding which
 * defaults count as "non-empty" is a per-field call this static scan can't make reliably, so
 * those stay a code-review concern instead (see CLAUDE.md's "Placeholder is not a label").
 *
 * Reads raw template source via Vite's `?raw` glob import rather than the compiled-component
 * glob `widgets.smoke.test.ts` uses, because a compiled component doesn't preserve which template
 * attributes were written — only that test can see the exact source text.
 */
import { describe, it, expect } from 'vitest';

const editWidgetSources = import.meta.glob('../components/Widgets/**/Edit*.vue', {
  query: '?raw',
  import: 'default',
  eager: true,
}) as Record<string, string>;

/** Every `<BaseInput ...>` / `<BaseSelect ...>` tag, matched non-greedily so one tag can't span into the next. */
function fieldTags(source: string): string[] {
  return [
    ...(source.match(/<BaseInput\b[\s\S]*?\/?>/g) ?? []),
    ...(source.match(/<BaseSelect\b[\s\S]*?\/?>/g) ?? []),
  ];
}

const files = Object.keys(editWidgetSources);

describe('Edit*Widget.vue gives every field a persistent label', () => {
  it('finds at least one Edit*Widget.vue to check', () => {
    // A glob that silently matches nothing would make every test below vacuously pass.
    expect(files.length).toBeGreaterThan(0);
  });

  it.each(files)('%s labels every BaseInput/BaseSelect', (file) => {
    const tags = fieldTags(editWidgetSources[file]);

    expect(tags.length, `${file}: expected to find BaseInput/BaseSelect fields`).toBeGreaterThan(0);

    const unlabelled = tags.filter((tag) => !/[\s:]label=/.test(tag));

    expect(unlabelled, `${file}: every field on an edit form is prefilled, so :placeholder alone is never enough`).toEqual([]);
  });
});
