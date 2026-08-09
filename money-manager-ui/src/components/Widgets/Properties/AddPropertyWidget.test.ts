/**
 * The reference implementation for rendering server-side validation errors.
 *
 * Two behaviours, and they only make sense together. The widget renders the messages the page
 * hands it against the inputs that caused them — and it no longer empties itself on submit.
 *
 * The second is the one worth a test. Clearing on emit was harmless while failures were
 * invisible: the API accepted almost anything, so a submit was as good as a success. Now that a
 * write can be rejected with a message per field, a form that empties itself on submit would
 * render those messages underneath inputs the user had just watched go blank — strictly worse
 * than showing nothing. Emptying is the page's job now, after a success, by remounting.
 */
import { describe, it, expect } from 'vitest';
import { mount } from '@vue/test-utils';
import AddPropertyWidget from './AddPropertyWidget.vue';

const fill = async (wrapper: ReturnType<typeof mount>, name: string, address: string) => {
  const inputs = wrapper.findAll('input');
  await inputs[0].setValue(name);
  await inputs[1].setValue(address);
};

describe('rendering server errors', () => {
  it('places a field message on the input that caused it', () => {
    const wrapper = mount(AddPropertyWidget, {
      props: { errors: { propertyName: 'The PropertyName field is required.' } },
    });

    expect(wrapper.text()).toContain('The PropertyName field is required.');
  });

  it('renders a message for every field that failed', () => {
    const wrapper = mount(AddPropertyWidget, {
      props: {
        errors: {
          propertyName: 'Name is required.',
          address: 'Address is required.',
          purchasePrice: 'PurchasePrice cannot be negative.',
        },
      },
    });

    const text = wrapper.text();

    expect(text).toContain('Name is required.');
    expect(text).toContain('Address is required.');
    expect(text).toContain('PurchasePrice cannot be negative.');
  });

  it('shows a general failure that belongs to no field', () => {
    const wrapper = mount(AddPropertyWidget, {
      props: { error: 'Something went wrong.' },
    });

    expect(wrapper.text()).toContain('Something went wrong.');
  });

  it('shows nothing before anything has been submitted', () => {
    expect(mount(AddPropertyWidget).text()).not.toContain('required');
  });
});

describe('what submitting does', () => {
  it('emits what was typed', async () => {
    const wrapper = mount(AddPropertyWidget);

    await fill(wrapper, 'Maple Court', '14 Maple Court');
    await wrapper.find('form').trigger('submit');

    const created = wrapper.emitted('create');

    expect(created).toHaveLength(1);
    expect(created?.[0][0]).toMatchObject({
      propertyName: 'Maple Court',
      address: '14 Maple Court',
    });
  });

  it('keeps what was typed, because it does not yet know the write succeeded', async () => {
    // The regression guard. If this widget ever clears itself on submit again, a rejected write
    // shows its messages against empty inputs and the user loses everything they entered.
    const wrapper = mount(AddPropertyWidget);

    await fill(wrapper, 'Maple Court', '14 Maple Court');
    await wrapper.find('form').trigger('submit');

    const inputs = wrapper.findAll('input');

    expect(inputs[0].element.value).toBe('Maple Court');
    expect(inputs[1].element.value).toBe('14 Maple Court');
  });
});
