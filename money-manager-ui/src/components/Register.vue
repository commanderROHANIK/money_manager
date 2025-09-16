<template>
  <div class="flex items-center justify-center min-h-screen bg-gray-100">
    <div class="bg-white p-8 rounded-lg shadow-lg w-full max-w-sm">
      <h2 class="text-2xl font-semibold mb-6 text-center">Register</h2>
      <form @submit.prevent="handleRegister" class="space-y-4">
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
          autocomplete="new-password"
        />
        <button
          type="submit"
          class="w-full bg-green-600 text-white py-2 rounded hover:bg-green-700 transition"
        >
          Register
        </button>
        <button
          type="button"
          class="w-full bg-white text-black py-2 rounded hover:bg-green-200 transition"
          @click="$router.push('/login')"
        >
          Login
        </button>
      </form>
      <p
        v-if="message"
        :class="{'text-green-600': message.includes('success'), 'text-red-600': !message.includes('success')}"
        class="mt-4 text-center"
      >
        {{ message }}
      </p>
    </div>
  </div>
</template>

<script>
import { register } from '../services/authService';

export default {
  data() {
    return { username: '', password: '', message: '' };
  },
  methods: {
    handleRegister() {
      register(this.username, this.password)
        .then(() => this.message = 'Registered successfully')
        .catch((err) => this.message = 'Error registering: ' + err.message);
    }
  }
};
</script>
