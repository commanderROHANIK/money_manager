<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('property.highestRent.title') }}</h2>

    <div v-if="mostExpensive" class="space-y-2">
      <router-link
        :to="`/properties/${mostExpensive.id}`"
        class="text-lg font-medium text-primary-strong hover:underline"
      >
        {{ mostExpensive.propertyName }}
      </router-link>
      <div class="text-sm text-text-muted">
        {{ mostExpensive.address }}
      </div>
      <div class="text-sm text-text-muted">
        {{ t('property.highestRent.monthlyRent') }}
        <span class="font-semibold text-primary-strong tabular-nums">
          {{ formatMoney(mostExpensive.rentAmount, mostExpensive.currencyCode) }}
        </span>
      </div>
      <Badge :variant="mostExpensive.isRented ? 'primary' : 'neutral'">
        {{ mostExpensive.isRented ? t('property.rented') : t('property.vacant') }}
      </Badge>
    </div>

    <div v-else class="text-sm text-text-muted">
      {{ t('property.highestRent.empty') }}
    </div>
  </div>
</template>

<script setup lang="ts">
import type { RentalProperty } from '../../../models/models';
import { computed } from 'vue';
import { formatMoney } from '../../../utils/money';
import Badge from '../../ui/Badge.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = defineProps<{
  properties: RentalProperty[];
}>();

const mostExpensive = computed(() =>
  props.properties.reduce((max, p) =>
    !max || p.rentAmount > max.rentAmount ? p : max, null as RentalProperty | null
  )
);
</script>
