<script setup>
import { session } from '../stores/session.js'
</script>

<template>
  <header class="header">
    <div class="inner">
      <RouterLink to="/" class="logo"><span class="w">Сервис</span><span class="b">Деск</span></RouterLink>
      <nav>
        <RouterLink to="/" :class="{ active: $route.name === 'orders' || $route.name === 'order' }">Заявки</RouterLink>
        <RouterLink to="/profile" :class="{ active: $route.name?.startsWith('profile') }">Профиль</RouterLink>
      </nav>
      <span class="user" v-if="session.user">{{ session.user.firstName }}</span>
    </div>
  </header>
</template>

<style scoped>
.header { background: #1B2A41; color: #fff; position: sticky; top: 0; z-index: 10; box-shadow: 0 2px 8px rgba(0,0,0,.15); }
.inner { max-width: 960px; margin: 0 auto; padding: 0 24px; height: 64px; display: flex; align-items: center; gap: 24px; }
.logo { font-size: 22px; font-weight: 700; letter-spacing: -.2px; }
.logo .w { color: #fff; }
.logo .b { color: #2D6CDF; }
nav { margin-left: auto; display: flex; gap: 22px; }
nav a { color: #9FB0C8; font-weight: 500; padding: 6px 0; border-bottom: 2px solid transparent; }
nav a:hover { color: #fff; }
nav a.active { color: #fff; font-weight: 600; border-bottom-color: #2D6CDF; }
.user { color: #9FB0C8; font-size: 13px; }
@media (max-width: 768px) { .inner { padding: 0 16px; } }
@media (max-width: 480px) { .user { display: none; } .logo { font-size: 20px; } nav { gap: 16px; } }
</style>
