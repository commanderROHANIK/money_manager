<template>
  <div>
    <h2 class="font-heading text-lg font-bold mb-4">{{ t('property.list.title') }}</h2>

    <p v-if="properties.length === 0" class="text-sm text-text-muted">
      {{ t('property.list.empty') }}
    </p>

    <ul v-else class="max-h-[360px] overflow-y-auto">
      <ListRow v-for="property in properties" :key="property.id">
        <template #title>
          <router-link
            :to="`/properties/${property.id}`"
            class="font-medium text-primary-strong hover:underline"
          >
            {{ property.propertyName }}
          </router-link>
        </template>
        <template #subtitle>
          <div class="text-sm text-text-muted truncate">
            {{ property.address }}
          </div>
          <div v-if="property.isRented" class="text-sm text-text-muted">
            {{ formatMoney(property.rentAmount, property.currencyCode) }} {{ t('property.perMonth') }}
            <span v-if="property.tenantName">· {{ property.tenantName }}</span>
          </div>
        </template>
        <template #trailing>
          <!-- Arrears first: it is the thing that needs acting on, and "Rented" next to two
               months of missing rent is the more comforting half of the truth. -->
          <Badge v-if="arrearsFor(property.id)" variant="danger">
            {{ arrearsLabel(property.id) }}
          </Badge>
          <Badge :variant="property.isRented ? 'primary' : 'neutral'">
            {{ property.isRented ? t('property.rented') : t('property.vacant') }}
          </Badge>
          <router-link
            :to="`/properties/${property.id}`"
            class="text-sm text-primary-strong hover:underline whitespace-nowrap"
          >
            {{ property.isRented ? t('property.manageRent') : t('property.setRent') }}
          </router-link>
          <button
            class="text-text-muted hover:text-danger text-sm"
            :aria-label="t('property.deleteAria', { name: property.propertyName })"
            @click="confirmDelete(property)"
          >
            {{ t('property.delete') }}
          </button>
        </template>
      </ListRow>
    </ul>
  </div>
</template>

<script setup lang="ts">
import type { PropertyArrears, RentalProperty } from '../../../models/models';
import { formatMoney } from '../../../utils/money';
import ListRow from '../../ui/ListRow.vue';
import Badge from '../../ui/Badge.vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const props = withDefaults(
  defineProps<{
    properties: RentalProperty[];
    /** Only properties that owe something appear here; absent means square. */
    arrears?: PropertyArrears[];
  }>(),
  { arrears: () => [] }
);

function arrearsFor(propertyId: number): PropertyArrears | undefined {
  return props.arrears.find((a) => a.propertyId === propertyId);
}

function arrearsLabel(propertyId: number): string {
  const owed = arrearsFor(propertyId);
  if (!owed) return '';

  return t(
    'property.rentCollection.behind',
    {
      amount: formatMoney(owed.arrears, owed.currencyCode),
      count: owed.overduePeriodCount,
    },
    owed.overduePeriodCount
  );
}

// The parent has always listened for this; the button that raises it was never built.
const emit = defineEmits<{ (e: 'delete-property', id: number): void }>();

function confirmDelete(property: RentalProperty) {
  // Deleting a property takes its tenancies, ledger and history with it, so this is worth
  // a confirmation step.
  const message = t('property.confirmDelete', { name: property.propertyName });

  if (window.confirm(message)) {
    emit('delete-property', property.id);
  }
}
</script>
