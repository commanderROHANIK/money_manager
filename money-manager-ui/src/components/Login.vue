<template>
  <div class="flex min-h-screen items-center justify-center bg-surface-2 p-6">
    <div class="flex w-full max-w-sm flex-col gap-5 rounded-xl bg-surface p-10 shadow-card">
      <div class="flex flex-col items-center gap-1 text-center">
        <div class="mb-2 h-10 w-10 rounded-md bg-primary"></div>
        <div class="font-heading text-xl font-extrabold">{{ $t('auth.welcomeBack') }}</div>
        <div class="text-sm text-text-muted">{{ $t('auth.welcomeBackHint') }}</div>
      </div>
      <form class="flex flex-col gap-4" @submit.prevent="handleLogin">
        <BaseInput v-model="username" :placeholder="$t('auth.username')" autocomplete="username" />
        <BaseInput v-model="password" type="password" :placeholder="$t('auth.password')" autocomplete="current-password" />
        <BaseButton type="submit" block>{{ $t('auth.logIn') }}</BaseButton>
        <BaseButton type="button" variant="secondary" block @click="$router.push('/register')">
          {{ $t('auth.register') }}
        </BaseButton>
      </form>
      <p v-if="errorKey" class="text-center text-sm text-danger">{{ $t(errorKey) }}</p>
    </div>
  </div>
</template>

<script lang="ts">
import { defineComponent } from 'vue';
import { login } from '../services/authService';
import BaseInput from './ui/BaseInput.vue';
import BaseButton from './ui/BaseButton.vue';

// defineComponent rather than a bare object literal: it is what gives `this` a type inside
// data() and methods(). Without it `lang="ts"` compiles but checks almost nothing.
export default defineComponent({
  components: { BaseInput, BaseButton },
  data() {
    return { username: '', password: '', errorKey: '' };
  },
  methods: {
    handleLogin() {
      login(this.username, this.password)
        .then(() => {
          this.$router.push('/'); 
          window.location.reload();
        })
        .catch(() => this.errorKey = 'auth.invalidLogin');
    }
  }
});
</script>
