// Форматирование дат, сумм и телефонов
export const date = v => v ? new Date(v).toLocaleDateString('ru-RU') : '—'
export const dateTime = v => v ? new Date(v).toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—'
export const money = v => `${Number(v || 0).toLocaleString('ru-RU', { minimumFractionDigits: 0, maximumFractionDigits: 2 })} ₽`

export function phone(p) {
  if (!p || p.length !== 12) return p || ''
  return `+7 ${p.slice(2, 5)} ${p.slice(5, 8)}-${p.slice(8, 10)}-${p.slice(10, 12)}`
}

export function plural(n, one, few, many) {
  const a = Math.abs(n) % 100, b = a % 10
  if (a > 10 && a < 20) return many
  if (b > 1 && b < 5) return few
  if (b === 1) return one
  return many
}

// Этапы шкалы прогресса (совпадают с OrderStatuses.Steps на сервере)
export const STEPS = [
  { code: 'accepted', title: 'Принята' },
  { code: 'diagnostics', title: 'Диагностика' },
  { code: 'approval', title: 'Согласование' },
  { code: 'in_work', title: 'В работе' },
  { code: 'ready', title: 'Готова' },
  { code: 'issued', title: 'Выдана' },
]

export function stepIndex(status) {
  if (status === 'waiting_parts') return 3
  return STEPS.findIndex(s => s.code === status)
}
