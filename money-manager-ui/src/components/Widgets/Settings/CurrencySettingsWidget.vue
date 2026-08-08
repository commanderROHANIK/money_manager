<template>
  <BaseCard title="Reporting currency">
    <p class="mb-4 text-sm text-text-muted">
      The currency consolidated totals are reported in. Each property and account keeps its own
      currency; this only decides the unit the portfolio and balance rollups add up to.
    </p>

    <div class="flex flex-wrap items-end gap-3">
      <BaseSelect v-model="baseCurrency" label="Base currency" class="w-40">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </BaseSelect>

      <BaseButton :disabled="saving" @click="save">Save</BaseButton>
    </div>

    <label class="mt-4 flex items-start gap-2.5 text-sm">
      <input v-model="alwaysConvert" type="checkbox" class="mt-0.5" />
      <span>
        <span class="font-semibold">Always convert to {{ baseCurrency }}</span>
        <span class="block text-text-muted">
          Off by default: totals stay in their own currency while everything shares one, so no
          exchange rate is needed. Turn this on to see every total in {{ baseCurrency }} — which
          means a rate is required for each other currency you hold.
        </span>
      </span>
    </label>

    <p v-if="message" class="mt-3 text-sm" :class="failed ? 'text-danger' : 'text-primary-strong'">
      {{ message }}
    </p>
  </BaseCard>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { fetchSettings, updateSettings } from '../../../services/settingsApi';
import { CURRENCIES } from '../../../utils/currencies';
import BaseButton from '../../ui/BaseButton.vue';
import BaseCard from '../../ui/BaseCard.vue';
import BaseSelect from '../../ui/BaseSelect.vue';

const emit = defineEmits<{ (e: 'saved'): void }>();

const baseCurrency = ref<string>('EUR');
const alwaysConvert = ref(false);
const saving = ref(false);
const message = ref('');
const failed = ref(false);

onMounted(async () => {
  try {
    const settings = await fetchSettings();
    baseCurrency.value = settings.baseCurrency;
    alwaysConvert.value = settings.alwaysConvertToBaseCurrency;
  } catch (error) {
    console.error('Failed to load settings:', error);
  }
});

async function save() {
  saving.value = true;
  message.value = '';

  try {
    const saved = await updateSettings({
      baseCurrency: baseCurrency.value,
      alwaysConvertToBaseCurrency: alwaysConvert.value,
    });

    baseCurrency.value = saved.baseCurrency;
    alwaysConvert.value = saved.alwaysConvertToBaseCurrency;
    failed.value = false;
    message.value = 'Saved.';
    emit('saved');
  } catch (error) {
    console.error('Failed to save settings:', error);
    failed.value = true;
    message.value = 'Could not save that. Please try again.';
  } finally {
    saving.value = false;
  }
}
</script>
