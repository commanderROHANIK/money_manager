<template>
  <BaseCard :title="t('settings.language')">
    <p class="mb-4 text-sm text-text-muted">{{ t('settings.languageHint') }}</p>

    <BaseSelect v-model="selected" :label="t('settings.language')" class="w-56">
      <option v-for="code in SUPPORTED_LOCALES" :key="code" :value="code">
        {{ t(`settings.languageName.${code}`) }}
      </option>
    </BaseSelect>
  </BaseCard>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import { setLocale } from '../../../i18n';
import { currentLocale, SUPPORTED_LOCALES } from '../../../i18n/locale';
import type { Locale } from '../../../i18n/locale';
import BaseCard from '../../ui/BaseCard.vue';
import BaseSelect from '../../ui/BaseSelect.vue';

const { t } = useI18n();

// Applied on selection rather than behind a Save button, unlike the currency setting next to it.
// The two are not the same kind of choice: base currency changes what the numbers mean and is
// worth confirming, while language changes only how they are written — and the result of picking
// it is immediately visible, so a Save step would ask for confirmation of something already seen.
//
// It is also the one setting that cannot be read back from the server, so there is nothing to
// save to. It lives in localStorage, per device.
const selected = computed<Locale>({
  get: () => currentLocale.value,
  set: (locale) => setLocale(locale),
});

// The language names are deliberately identical in both locale files — a language is written in
// its own language wherever it appears, so someone who has accidentally switched to a language
// they cannot read can still find their way back.
</script>
