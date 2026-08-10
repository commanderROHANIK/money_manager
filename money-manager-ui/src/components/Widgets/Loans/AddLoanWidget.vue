<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-1">{{ t('loan.add.title') }}</h2>
    <p class="text-xs text-text-muted mb-4">
      Track what you owe — the amounts below are what interest and payoff progress are measured
      against.
    </p>

    <form class="space-y-3" @submit.prevent="submit">
      <BaseInput v-model.trim="form.loanName" :placeholder="t('loan.add.name')" required />

      <div>
        <BaseInput
          v-model.number="form.loanAmount"
          :placeholder="t('loan.add.originalAmount')"
          type="number"
          min="0"
          required
        />
        <p class="text-xs text-text-muted mt-1">{{ t('loan.add.originalAmountHint') }}</p>
      </div>

      <div>
        <BaseInput
          v-model.number="form.remainingBalance"
          :placeholder="t('loan.add.remainingBalance')"
          type="number"
          min="0"
          required
        />
        <p class="text-xs text-text-muted mt-1">
          What's still owed today — this is what interest and payoff progress are calculated from.
        </p>
      </div>

      <div>
        <BaseInput
          v-model.number="form.interestRate"
          :placeholder="t('loan.add.interestRate')"
          type="number"
          min="0"
          step="0.01"
          required
        />
        <p class="text-xs text-text-muted mt-1">{{ t('loan.add.interestRateHint') }}</p>
      </div>

      <div>
        <BaseInput v-model="form.dueDate" type="date" required />
        <p class="text-xs text-text-muted mt-1">{{ t('loan.add.dueDateHint') }}</p>
      </div>

      <div>
        <BaseSelect v-model="form.currencyCode">
          <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
        </BaseSelect>
        <p class="text-xs text-text-muted mt-1">{{ t('loan.add.currencyHint') }}</p>
      </div>

      <div>
        <label class="flex items-center gap-1.5 text-sm text-text">
          <input v-model="form.isPaidOff" type="checkbox" class="accent-primary" />
          <span>{{ t('loan.add.paidOffLabel') }}</span>
        </label>
        <p class="text-xs text-text-muted mt-1">{{ t('loan.add.paidOffHint') }}</p>
      </div>

      <BaseButton type="submit">{{ t('loan.add.submit') }}</BaseButton>
    </form>
  </div>
</template>

<script setup lang="ts">
import { reactive } from 'vue';
import type { Loan } from '../../../models/models';
import { CURRENCIES } from '../../../utils/currencies';
import BaseInput from '../../ui/BaseInput.vue';
import BaseSelect from '../../ui/BaseSelect.vue';
import BaseButton from '../../ui/BaseButton.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const emit = defineEmits<{ (e: 'create', payload: Omit<Loan, 'id'>): void }>();

function emptyForm() {
  return {
    loanName: '',
    loanAmount: 0,
    remainingBalance: 0,
    interestRate: 0,
    dueDate: '',
    isPaidOff: false,
    currencyCode: 'EUR',
  };
}

const form = reactive(emptyForm());

function submit() {
  emit('create', { ...form });
  Object.assign(form, emptyForm());
}
</script>
