/**
 * BaseInput does two things that look like mistakes and are not, so they are pinned here.
 *
 * `inheritAttrs: false` plus a manual attribute split: class/style go to the wrapping <label>
 * because that is the element the parent lays out (a grid cell or flex item — `col-span-2` has
 * to land there to do anything), while everything else belongs on the control. Someone
 * "simplifying" this by deleting inheritAttrs would silently move layout classes onto the input
 * and break every form's layout without failing a type check.
 *
 * The number modifier emits null rather than 0 for an empty field, which matters because this
 * app treats null as "not known" and 0 as a real amount.
 */
import { describe, it, expect } from 'vitest';
import { mount } from '@vue/test-utils';
import BaseInput from './BaseInput.vue';

/** The most recent emission. Indexed rather than `.at(-1)`, which this project's lib predates. */
const lastEmit = (emitted: unknown[][] | undefined) => emitted?.[emitted.length - 1];

describe('attribute split', () => {
  it('puts class and style on the label, not the input', () => {
    const wrapper = mount(BaseInput, {
      attrs: { class: 'col-span-2', style: 'width: 10px' },
    });

    const label = wrapper.get('label');
    expect(label.classes()).toContain('col-span-2');
    expect(label.attributes('style')).toContain('width: 10px');

    const input = wrapper.get('input');
    expect(input.classes()).not.toContain('col-span-2');
    expect(input.attributes('style')).toBeUndefined();
  });

  it('puts every other attribute on the input, not the label', () => {
    const wrapper = mount(BaseInput, {
      attrs: { required: '', min: '0.01', step: '0.01', 'aria-label': 'Amount' },
    });

    const input = wrapper.get('input');
    expect(input.attributes('required')).toBeDefined();
    expect(input.attributes('min')).toBe('0.01');
    expect(input.attributes('step')).toBe('0.01');
    expect(input.attributes('aria-label')).toBe('Amount');

    expect(wrapper.get('label').attributes('required')).toBeUndefined();
  });
});

describe('v-model', () => {
  it('emits the raw string by default', async () => {
    const wrapper = mount(BaseInput);

    await wrapper.get('input').setValue('hello');

    expect(lastEmit(wrapper.emitted('update:modelValue'))).toEqual(['hello']);
  });

  it('emits a number when the number modifier is set', async () => {
    const wrapper = mount(BaseInput, { props: { modelModifiers: { number: true } } });

    await wrapper.get('input').setValue('42');

    expect(lastEmit(wrapper.emitted('update:modelValue'))).toEqual([42]);
  });

  it('emits null, not 0, when a numeric field is cleared', async () => {
    // 0 is a real amount in this app and null means "not entered". Collapsing the two here
    // would let an empty field be recorded as a zero-value transaction.
    const wrapper = mount(BaseInput, { props: { modelModifiers: { number: true } } });

    await wrapper.get('input').setValue('');

    expect(lastEmit(wrapper.emitted('update:modelValue'))).toEqual([null]);
  });

  it('trims when the trim modifier is set', async () => {
    const wrapper = mount(BaseInput, { props: { modelModifiers: { trim: true } } });

    await wrapper.get('input').setValue('  padded  ');

    expect(lastEmit(wrapper.emitted('update:modelValue'))).toEqual(['padded']);
  });

  it('renders a null model value as an empty field rather than the text "null"', async () => {
    const wrapper = mount(BaseInput, { props: { modelValue: null } });

    expect(wrapper.get('input').element.value).toBe('');
  });
});

describe('label and error', () => {
  it('renders the label only when one is given', () => {
    expect(mount(BaseInput).find('span').exists()).toBe(false);
    expect(mount(BaseInput, { props: { label: 'Amount' } }).text()).toContain('Amount');
  });

  it('shows the error message and marks the control', () => {
    const wrapper = mount(BaseInput, { props: { label: 'Amount', error: 'Required' } });

    expect(wrapper.text()).toContain('Required');
    expect(wrapper.get('input').classes().join(' ')).toContain('border-danger');
  });
});
