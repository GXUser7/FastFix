<script setup>
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api/http.js'
import { setSession } from '../stores/session.js'

const router = useRouter()
const f = reactive({ lastName: '', firstName: '', phone: '', email: '', password: '', password2: '' })
const error = ref('')
const busy = ref(false)

function validate() {
  if (!f.lastName.trim() || !f.firstName.trim()) return 'Укажите фамилию и имя'
  if (f.phone.replace(/\D/g, '').length < 10) return 'Укажите телефон в формате +7 900 123-45-67'
  if (f.email && !/^\S+@\S+\.\S+$/.test(f.email)) return 'Некорректный email'
  if (f.password.length < 8 || !/\d/.test(f.password) || !/\p{L}/u.test(f.password))
    return 'Пароль должен содержать не менее 8 символов, буквы и цифры'
  if (f.password !== f.password2) return 'Пароли не совпадают'
  return ''
}

async function submit() {
  error.value = validate()
  if (error.value) return
  busy.value = true
  try {
    const auth = await api.post('/auth/register', {
      lastName: f.lastName.trim(), firstName: f.firstName.trim(), phone: f.phone, email: f.email.trim() || null, password: f.password,
    })
    setSession(auth)
    router.replace('/')
  } catch (e) {
    error.value = e.message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="auth">
    <form class="panel" @submit.prevent="submit" novalidate>
      <div class="logo"><span class="w">Сервис</span><span class="b">Деск</span></div>
      <h1>Регистрация</h1>
      <p class="sub">Логином служит номер телефона или email</p>
      <div v-if="error" class="error-box" role="alert">{{ error }}</div>
      <div class="row">
        <input class="dark-input" v-model="f.lastName" placeholder="Фамилия" autocomplete="family-name" />
        <input class="dark-input" v-model="f.firstName" placeholder="Имя" autocomplete="given-name" />
      </div>
      <input class="dark-input" v-model="f.phone" placeholder="Телефон, +7 900 123-45-67" type="tel" autocomplete="tel" />
      <input class="dark-input" v-model="f.email" placeholder="Email (необязательно)" type="email" autocomplete="email" />
      <input class="dark-input" v-model="f.password" type="password" placeholder="Пароль (от 8 символов, буквы и цифры)" autocomplete="new-password" />
      <input class="dark-input" v-model="f.password2" type="password" placeholder="Повторите пароль" autocomplete="new-password" />
      <button class="btn block" :disabled="busy">{{ busy ? 'Регистрируем…' : 'Зарегистрироваться' }}</button>
      <p class="links">Уже есть учётная запись? <RouterLink to="/login">Войти</RouterLink></p>
    </form>
  </div>
</template>

<style scoped>
.auth { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px 16px; background: #F4F6F8; }
.panel { width: 100%; max-width: 480px; background: #1B2A41; border-radius: 8px; padding: 40px; box-shadow: 0 10px 40px rgba(27, 42, 65, .25); color: #fff; }
.logo { font-size: 26px; font-weight: 700; margin-bottom: 32px; }
.logo .b { color: #2D6CDF; }
h1 { font-size: 24px; margin-bottom: 8px; }
.sub { color: #9FB0C8; margin: 0 0 24px; }
.row { gap: 12px; }
.dark-input {
  display: block; width: 100%; height: 48px; margin-bottom: 14px; padding: 0 16px; border: 1px solid transparent;
  border-radius: 6px; background: #24344F; color: #fff; font: inherit; font-size: 15px; outline: none;
}
.dark-input::placeholder { color: #8394AD; }
.dark-input:focus { border-color: #2D6CDF; }
.btn { height: 50px; font-size: 15px; margin-top: 8px; }
.links { text-align: center; color: #9FB0C8; margin: 18px 0 0; }
.links a { color: #fff; font-weight: 600; }
@media (max-width: 480px) { .panel { padding: 30px 22px; } }
</style>
