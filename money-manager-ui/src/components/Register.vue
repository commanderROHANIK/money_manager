<template>
  <div class="flex min-h-screen items-center justify-center bg-surface-2 p-6">
    <div class="flex w-full max-w-sm flex-col gap-5 rounded-xl bg-surface p-10 shadow-card">
      <div class="flex flex-col items-center gap-1 text-center">
        <div class="mb-2 h-10 w-10 rounded-md bg-primary"></div>
        <div class="font-heading text-xl font-extrabold">{{ $t('auth.createAccount') }}</div>
        <div class="text-sm text-text-muted">{{ $t('auth.createAccountHint') }}</div>
      </div>

      <form class="flex flex-col gap-4" @submit.prevent="handleRegister">
        <BaseInput v-model.trim="username" :placeholder="$t('auth.username')" autocomplete="username" required />
        <BaseInput v-model.trim="email" type="email" :placeholder="$t('auth.email')" autocomplete="email" required />
        <BaseInput v-model="password" type="password" :placeholder="$t('auth.password')" autocomplete="new-password" required />
        <BaseInput
          v-model="confirmPassword"
          type="password"
          :placeholder="$t('auth.confirmPassword')"
          autocomplete="new-password"
          required
        />

        <BaseButton type="submit" block :disabled="loading">
          {{ loading ? $t('auth.registering') : $t('auth.register') }}
        </BaseButton>
        <BaseButton type="button" variant="secondary" block @click="$router.push('/login')">
          {{ $t('auth.logIn') }}
        </BaseButton>
      </form>

      <p
        v-if="messageKey"
        class="text-center text-sm"
        :class="failed ? 'text-danger' : 'text-primary-strong'"
      >
        {{ $t(messageKey, messageArgs) }}
      </p>
    </div>
  </div>
</template>

<script lang="ts">
import { defineComponent } from 'vue';
import axios from 'axios';
import { register } from "../services/authService";
import BaseInput from './ui/BaseInput.vue';
import BaseButton from './ui/BaseButton.vue';

// defineComponent rather than a bare object literal: it is what gives `this` a type inside
// data() and methods(). Without it `lang="ts"` compiles but checks almost nothing.
export default defineComponent({
  components: { BaseInput, BaseButton },
  data() {
    return {
      username: "",
      email: "",
      password: "",
      confirmPassword: "",
      messageKey: "",
      messageArgs: {} as Record<string, string>,
      failed: false,
      loading: false,
    };
  },
  methods: {
    async handleRegister() {
      if (this.password !== this.confirmPassword) {
        this.messageKey = "auth.passwordsDoNotMatch";
        this.failed = true;
        return;
      }

      this.loading = true;
      this.messageKey = "";

      try {
        await register(this.username, this.email, this.password);
        this.messageKey = "auth.registeredSuccessfully";
        this.failed = false;
        this.username = this.email = this.password = this.confirmPassword = "";
      } catch (err) {
        // A deployment can close registration, in which case the endpoint is not there at all —
        // 404 rather than 403, so it does not advertise itself. Without this branch the page
        // renders "Error registering: Request failed with status code 404", which reads like the
        // app is broken rather than like a deliberate setting.
        this.failed = true;

        if (axios.isAxiosError(err) && err.response?.status === 404) {
          this.messageKey = "auth.registrationClosed";
          this.messageArgs = {};
        } else {
          this.messageKey = "auth.registerFailed";
          // The server's own message is English — the API is not localised — so it is passed
          // through as data rather than translated. Only the sentence around it changes language.
          this.messageArgs = {
            reason: err instanceof Error ? err.message : this.$t("auth.registerFailedFallback"),
          };
        }
      } finally {
        this.loading = false;
      }
    },
  },
});
</script>
