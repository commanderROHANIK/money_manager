<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('bankAccount.add.title') }}</h2>

    <form class="space-y-3" @submit.prevent="submit">
      <BaseInput v-model.trim="form.accountName" :placeholder="t('bankAccount.add.accountName')" required />
      <BaseInput v-model.trim="form.bankName" :placeholder="t('bankAccount.add.bankName')" required />
      <BaseInput v-model.trim="form.accountNumber" :placeholder="t('bankAccount.add.accountNumber')" required />

      <div>
        <BaseInput v-model.trim="form.accountType" :placeholder="t('bankAccount.add.accountType')" required />
        <p class="text-xs text-text-muted mt-1">{{ t('bankAccount.add.accountTypeHint') }}</p>
      </div>

      <BaseInput
        v-model.number="form.balance"
        :placeholder="t('bankAccount.add.balance')"
        type="number"
        min="0"
        step="0.01"
        required
      />

      <div>
        <BaseSelect v-model="form.currencyCode">
          <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
        </BaseSelect>
        <p class="text-xs text-text-muted mt-1">{{ t('bankAccount.add.currencyHint') }}</p>
      </div>

      <BaseButton type="submit">{{ t('bankAccount.add.submit') }}</BaseButton>
    </form>
  </div>
</template>

<script setup lang="ts">
import { reactive } from 'vue';
import { useI18n } from 'vue-i18n';
import type { BankAccount } from '../../../models/models';
import { CURRENCIES } from '../../../utils/currencies';
import BaseInput from '../../ui/BaseInput.vue';
import BaseSelect from '../../ui/BaseSelect.vue';
import BaseButton from '../../ui/BaseButton.vue';

const { t } = useI18n();

const emit = defineEmits<{ (e: 'create', payload: Omit<BankAccount, 'id'>): void }>();

function emptyForm() {
  return {
    accountName: '',
    bankName: '',
    accountNumber: '',
    accountType: '',
    balance: 0,
    currencyCode: 'EUR',
  };
}

const form = reactive(emptyForm());

function submit() {
  emit('create', { ...form });
  Object.assign(form, emptyForm());
}
</script>
