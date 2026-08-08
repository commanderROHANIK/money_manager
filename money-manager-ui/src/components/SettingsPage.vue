<template>
  <div class="grid grid-cols-1 xl:grid-cols-2 gap-4 p-4">
    <CurrencySettingsWidget @saved="bump" />
    <ExchangeRatesWidget :key="version" @changed="bump" />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import CurrencySettingsWidget from './Widgets/Settings/CurrencySettingsWidget.vue';
import ExchangeRatesWidget from './Widgets/Settings/ExchangeRatesWidget.vue';

// Changing the base currency changes which pairs matter, so the rate list is remounted rather
// than left showing a table that no longer answers the question being asked of it.
const version = ref(0);

function bump() {
  version.value += 1;
}
</script>
