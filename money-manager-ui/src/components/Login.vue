<template>
  <div class="flex items-center justify-center min-h-screen bg-gray-100">
    <div class="bg-white p-8 rounded-lg shadow-lg w-full max-w-sm">
      <h2 class="text-2xl font-semibold mb-6 text-center">Login</h2>
      <form @submit.prevent="handleLogin" class="space-y-4">
        <input
          v-model="username"
          placeholder="Username"
          class="w-full px-4 py-2 border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-green-500"
          autocomplete="username"
        />
        <input
          v-model="password"
          type="password"
          placeholder="Password"
          class="w-full px-4 py-2 border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-green-500"
          autocomplete="current-password"
        />
        <button
          type="submit"
          class="w-full bg-green-600 text-white py-2 rounded hover:bg-green-700 transition"
        >
          Login
        </button>
        <button
          type="button"
          class="w-full bg-white text-black py-2 rounded hover:bg-green-200 transition"
          @click="$router.push('/register')"
        >
          Register
        </button>
      </form>
      <p v-if="error" class="mt-4 text-red-600 text-center">{{ error }}</p>
    </div>
  </div>
</template>

<script>
import { login } from '../services/authService';

export default {
  data() {
    return { username: '', password: '', error: '' };
  },
  methods: {
    handleLogin() {
      login(this.username, this.password)
        .then(() => this.$router.push('/'))
        .catch(() => this.error = 'Invalid login');

        window.location.reload();
    }
  }
};
</script>
