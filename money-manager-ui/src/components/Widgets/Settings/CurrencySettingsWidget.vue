<template>
  <BaseCard :title="t('settings.currencyTitle')">
    <p class="mb-4 text-sm text-text-muted">
      {{ t('settings.currencyIntro') }}
    </p>

    <div class="flex flex-wrap items-end gap-3">
      <BaseSelect v-model="baseCurrency" :label="t('settings.baseCurrency')" class="w-40">
        <option v-for="code in CURRENCIES" :key="code" :value="code">{{ code }}</option>
      </BaseSelect>

      <BaseButton :disabled="saving" @click="save">{{ t('settings.save') }}</BaseButton>
    </div>

    <label class="mt-4 flex items-start gap-2.5 text-sm">
      <input v-model="alwaysConvert" type="checkbox" class="mt-0.5" />
      <span>
        <span class="font-semibold">{{ t('settings.alwaysConvert', { currency: baseCurrency }) }}</span>
        <span class="block text-text-muted">
          {{ t('settings.alwaysConvertHint', { currency: baseCurrency }) }}
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
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

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
    message.value = t('settings.saved');
    emit('saved');
  } catch (error) {
    console.error('Failed to save settings:', error);
    failed.value = true;
    message.value = t('settings.saveFailed');
  } finally {
    saving.value = false;
  }
}
</script>
