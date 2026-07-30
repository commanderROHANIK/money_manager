<template>
  <div class="flex min-h-screen items-center justify-center bg-surface-2 p-6">
    <div class="flex w-full max-w-sm flex-col gap-5 rounded-xl bg-surface p-10 shadow-card">
      <div class="flex flex-col items-center gap-1 text-center">
        <div class="mb-2 h-10 w-10 rounded-md bg-primary"></div>
        <div class="font-heading text-xl font-extrabold">Create an account</div>
        <div class="text-sm text-text-muted">Let's get your money tracked.</div>
      </div>

      <form @submit.prevent="handleRegister" class="flex flex-col gap-4">
        <BaseInput v-model.trim="username" placeholder="Username" autocomplete="username" required />
        <BaseInput v-model.trim="email" type="email" placeholder="Email" autocomplete="email" required />
        <BaseInput v-model="password" type="password" placeholder="Password" autocomplete="new-password" required />
        <BaseInput
          v-model="confirmPassword"
          type="password"
          placeholder="Confirm Password"
          autocomplete="new-password"
          required
        />

        <BaseButton type="submit" block :disabled="loading">
          {{ loading ? "Registering..." : "Register" }}
        </BaseButton>
        <BaseButton type="button" variant="secondary" block @click="$router.push('/login')">
          Login
        </BaseButton>
      </form>

      <p
        v-if="message"
        class="text-center text-sm"
        :class="message.includes('success') ? 'text-primary' : 'text-danger'"
      >
        {{ message }}
      </p>
    </div>
  </div>
</template>

<script>
import { register } from "../services/authService";
import BaseInput from './ui/BaseInput.vue';
import BaseButton from './ui/BaseButton.vue';

export default {
  components: { BaseInput, BaseButton },
  data() {
    return {
      username: "",
      email: "",
      password: "",
      confirmPassword: "",
      message: "",
      loading: false,
    };
  },
  methods: {
    async handleRegister() {
      if (this.password !== this.confirmPassword) {
        this.message = "Passwords do not match";
        return;
      }

      this.loading = true;
      this.message = "";

      try {
        await register(this.username, this.email, this.password);
        this.message = "Registered successfully";
        this.username = this.email = this.password = this.confirmPassword = "";
      } catch (err) {
        this.message = "Error registering: " + err.message;
      } finally {
        this.loading = false;
      }
    },
  },
};
</script>