<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('bankAccount.edit.title') }}</h2>

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
        step="0.01"
        required
      />

      <div>
        <BaseSelect v-model="form.currencyCode">
          <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
        </BaseSelect>
        <p class="text-xs text-text-muted mt-1">{{ t('bankAccount.add.currencyHint') }}</p>
      </div>

      <BaseButton type="submit">{{ t('bankAccount.edit.submit') }}</BaseButton>
    </form>
  </div>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import type { BankAccount } from '../../../models/models';
import { CURRENCIES } from '../../../utils/currencies';
import BaseInput from '../../ui/BaseInput.vue';
import BaseSelect from '../../ui/BaseSelect.vue';
import BaseButton from '../../ui/BaseButton.vue';

const { t } = useI18n();

const props = defineProps<{ account: BankAccount }>();
const emit = defineEmits<{ (e: 'update', payload: BankAccount): void }>();

function fromAccount(account: BankAccount) {
  return {
    accountName: account.accountName,
    bankName: account.bankName,
    accountNumber: account.accountNumber,
    accountType: account.accountType,
    balance: account.balance,
    currencyCode: account.currencyCode,
  };
}

const form = reactive(fromAccount(props.account));

// The list row a modal was opened from can itself change id (e.g. the parent re-fetches while
// the modal is open), so the form tracks whichever account it was handed rather than only
// seeding itself once at mount.
watch(
  () => props.account,
  (account) => Object.assign(form, fromAccount(account)),
);

function submit() {
  emit('update', { id: props.account.id, ...form });
}
</script>
