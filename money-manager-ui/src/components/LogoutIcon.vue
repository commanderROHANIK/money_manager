<!-- components/LogoutIcon.vue -->
<template>
  <svg
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    xmlns="http://www.w3.org/2000/svg"
    role="img"
    :aria-label="ariaLabel"
    :style="{ color }"
  >
    <!-- "door" -->
    <path fill="currentColor" d="M11 3h8a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-8v-2h8V5h-8V3z"/>
    <!-- "arrow exit" (flipped to point left) -->
    <path fill="currentColor" d="M4 12l4 4v-3h6v-2H8V8l-4 4z"/>
  </svg>
</template>

<script>
export default {
  name: "LogoutIcon",
  props: {
    size: { type: [Number, String], default: 24 },      // width/height in px
    color: { type: String, default: "#fff" },           // default: white
    ariaLabel: { type: String, default: "Logout" }
  },
  onClick() {
    this.$emit('click');
    this.$router.push('/login');
  }
};
</script>
