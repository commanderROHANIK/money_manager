<template>
  <div class="flex items-center justify-center min-h-screen bg-gray-100">
    <div class="bg-white p-8 rounded-lg shadow-lg w-full max-w-sm">
      <h2 class="text-2xl font-semibold mb-6 text-center">Register</h2>

      <form @submit.prevent="handleRegister" class="space-y-4 p-0">
        <!-- Username -->
        <input
          v-model.trim="username"
          placeholder="Username"
          class="w-full px-4 py-2 border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-green-500"
          autocomplete="username"
          required
        />

        <!-- Email -->
        <input
          v-model.trim="email"
          type="email"
          placeholder="Email"
          class="w-full px-4 py-2 border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-green-500"
          autocomplete="email"
          required
        />

        <!-- Password -->
        <input
          v-model="password"
          type="password"
          placeholder="Password"
          class="w-full px-4 py-2 border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-green-500"
          autocomplete="new-password"
          required
        />

        <!-- Confirm Password -->
        <input
          v-model="confirmPassword"
          type="password"
          placeholder="Confirm Password"
          class="w-full px-4 py-2 border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-green-500"
          autocomplete="new-password"
          required
        />

        <!-- Register Button -->
        <button
          type="submit"
          class="w-full bg-green-600 text-white py-2 rounded hover:bg-green-700 transition disabled:opacity-50"
          :disabled="loading"
        >
          {{ loading ? "Registering..." : "Register" }}
        </button>

        <!-- Link to Login -->
        <button
          type="button"
          class="w-full bg-white text-black py-2 rounded hover:bg-green-200 transition"
          @click="$router.push('/login')"
        >
          Login
        </button>
      </form>

      <!-- Error/Success Message -->
      <p
        v-if="message"
        :class="{
          'text-green-600': message.includes('success'),
          'text-red-600': !message.includes('success')
        }"
        class="mt-4 text-center"
      >
        {{ message }}
      </p>
    </div>
  </div>
</template>

<script>
import { register } from "../services/authService";

export default {
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