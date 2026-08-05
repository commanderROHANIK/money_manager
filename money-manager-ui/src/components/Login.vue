<template>
  <div class="flex min-h-screen items-center justify-center bg-surface-2 p-6">
    <div class="flex w-full max-w-sm flex-col gap-5 rounded-xl bg-surface p-10 shadow-card">
      <div class="flex flex-col items-center gap-1 text-center">
        <div class="mb-2 h-10 w-10 rounded-md bg-primary"></div>
        <div class="font-heading text-xl font-extrabold">Welcome back</div>
        <div class="text-sm text-text-muted">Good to see you — let's check your money.</div>
      </div>
      <form @submit.prevent="handleLogin" class="flex flex-col gap-4">
        <BaseInput v-model="username" placeholder="Username" autocomplete="username" />
        <BaseInput v-model="password" type="password" placeholder="Password" autocomplete="current-password" />
        <BaseButton type="submit" block>Log in</BaseButton>
        <BaseButton type="button" variant="secondary" block @click="$router.push('/register')">
          Register
        </BaseButton>
      </form>
      <p v-if="error" class="text-center text-sm text-danger">{{ error }}</p>
    </div>
  </div>
</template>

<script>
import { login } from '../services/authService';
import BaseInput from './ui/BaseInput.vue';
import BaseButton from './ui/BaseButton.vue';

export default {
  components: { BaseInput, BaseButton },
  data() {
    return { username: '', password: '', error: '' };
  },
  methods: {
    handleLogin() {
      login(this.username, this.password)
        .then(() => {
          this.$router.push('/'); 
          window.location.reload();
        })
        .catch(() => this.error = 'Invalid login');
    }
  }
};
</script>
