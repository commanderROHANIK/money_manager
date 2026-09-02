<template>
  <div class="grid grid-cols-1 xl:grid-cols-2 gap-4 p-4">
    <LanguageSettingsWidget />
    <CurrencySettingsWidget @saved="bump" />
    <ExchangeRatesWidget :key="version" />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import CurrencySettingsWidget from './Widgets/Settings/CurrencySettingsWidget.vue';
import LanguageSettingsWidget from './Widgets/Settings/LanguageSettingsWidget.vue';
import ExchangeRatesWidget from './Widgets/Settings/ExchangeRatesWidget.vue';

// Changing the base currency changes which pairs matter, so the rate list is remounted rather
// than left showing a table that no longer answers the question being asked of it. Only
// CurrencySettingsWidget's own `@saved` bumps this — ExchangeRatesWidget already keeps itself in
// sync after its own actions (it calls its own `load()`), and wiring its `@changed` to the same
// remount used to wipe its local state (the add-rate form, including the "use the live rate"
// checkbox) straight back to defaults the moment an action succeeded, on top of fetching the rate
// list twice for nothing.
const version = ref(0);

function bump() {
  version.value += 1;
}
</script>
