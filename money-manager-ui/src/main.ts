import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import { i18n, initLocale } from './i18n'

// Before mounting, so the first paint is already in the right language. Applied after the app is
// created and the router installed would give a visible flash of the default locale on every
// load for anyone who chose the other one.
initLocale();

createApp(App)
    .use(i18n)
    .use(router)
    .mount('#app');
