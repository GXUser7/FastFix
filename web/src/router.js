// Маршруты и защита страниц, требующих входа
import { createRouter, createWebHistory } from 'vue-router'
import { isLoggedIn } from './stores/session.js'

const routes = [
  { path: '/login', name: 'login', component: () => import('./views/LoginView.vue'), meta: { guest: true } },
  { path: '/register', name: 'register', component: () => import('./views/RegisterView.vue'), meta: { guest: true } },
  { path: '/', name: 'orders', component: () => import('./views/OrdersView.vue') },
  { path: '/orders/:id(\\d+)', name: 'order', component: () => import('./views/OrderView.vue'), props: r => ({ id: Number(r.params.id) }) },
  { path: '/profile', name: 'profile', component: () => import('./views/ProfileView.vue') },
  { path: '/profile/edit', name: 'profile-edit', component: () => import('./views/ProfileEditView.vue') },
  { path: '/:pathMatch(.*)*', redirect: '/' },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior: () => ({ top: 0 }),
})

router.beforeEach(to => {
  const logged = isLoggedIn()
  if (!to.meta.guest && !logged) return { name: 'login', query: to.fullPath !== '/' ? { next: to.fullPath } : {} }
  if (to.meta.guest && logged) return { name: 'orders' }
})

export default router
