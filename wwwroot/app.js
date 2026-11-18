// Спільне для сторінок каталогу (index, master, login, account): запити до API,
// шапка з входом, повідомлення й дрібні помічники для дат, цін і відмінків.
'use strict';

// ───────────── Сервер ─────────────
class ApiError extends Error {
  constructor(status, message, data) {
    super(message);
    this.status = status; // 0 — немає зв'язку
    this.data = data;
  }
}

async function api(path, { method = 'GET', body } = {}) {
  let response;
  try {
    response = await fetch(`/api${path}`, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0, 'Немає зв’язку із сервером. Перевірте інтернет і спробуйте ще раз.');
  }
  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    throw new ApiError(response.status, data?.error ?? 'Сервер відповів помилкою. Спробуйте ще раз.', data);
  }
  return data;
}

// ───────────── Хто увійшов ─────────────
let currentUser = null;

/** Завантажує користувача й малює шапку. Повертає user або null. */
async function loadMe() {
  try {
    currentUser = (await api('/auth/me')).user;
  } catch {
    currentUser = null;
  }
  renderAuth();
  return currentUser;
}

function renderAuth() {
  const box = document.getElementById('auth');
  if (!box) return;
  if (!currentUser) {
    const next = encodeURIComponent(location.pathname.replace(/^\//, '') + location.search + location.hash);
    box.replaceChildren(h('a', { className: 'btn btn-quiet', href: `login.html?next=${next}` }, 'Увійти'));
    return;
  }
  const logout = h('button', { className: 'btn btn-quiet', type: 'button' }, 'Вийти');
  logout.addEventListener('click', async () => {
    logout.disabled = true;
    try {
      await api('/auth/logout', { method: 'POST' });
      location.href = './';
    } catch (error) {
      logout.disabled = false;
      toast(error.message);
    }
  });
  const label = currentUser.role === 'master' ? 'Кабінет майстра' : 'Мої записи';
  box.replaceChildren(
    h('a', { className: 'account-link', href: 'account.html', title: currentUser.name },
      h('span', { className: 'avatar avatar-sm', ariaHidden: 'true' }, initials(currentUser.name)),
      h('span', { className: 'account-label' }, label)),
    logout,
  );
}

// ───────────── DOM ─────────────
const $ = (selector, root = document) => root.querySelector(selector);

function h(tag, props = {}, ...children) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(props)) {
    if (value === undefined || value === null || value === false) continue;
    if (key === 'dataset') Object.assign(el.dataset, value);
    else if (key.startsWith('aria')) el.setAttribute(`aria-${key.slice(4).toLowerCase()}`, value); // ariaLabel → aria-label
    else if (key === 'role' || key === 'for') el.setAttribute(key, value);
    else el[key] = value;
  }
  el.append(...children.flat(Infinity).filter((c) => c !== null && c !== undefined && c !== false));
  return el;
}

let toastTimer;
function toast(text) {
  let el = document.getElementById('toast');
  if (!el) {
    el = h('div', { id: 'toast', className: 'toast', role: 'status', ariaLive: 'polite' });
    document.body.append(el);
  }
  el.textContent = text;
  el.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => el.classList.remove('show'), 4500);
}

// ───────────── Текст ─────────────
/** Відмінки: plural(3, 'відгук', 'відгуки', 'відгуків') → «відгуки». */
function plural(n, one, few, many) {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return one;
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few;
  return many;
}

const count = (n, one, few, many) => `${n} ${plural(n, one, few, many)}`;

function initials(name) {
  return (name || '?').split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0].toUpperCase()).join('');
}

const moneyFormat = new Intl.NumberFormat('uk-UA');
const money = (n) => `${moneyFormat.format(n)} грн`;

function duration(minutes) {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (!hours) return `${rest} хв`;
  return rest ? `${hours} год ${rest} хв` : `${hours} год`;
}

/** «★★★★☆» для рейтингу; поруч завжди число, тож зірки лише ілюстрація. */
function stars(rating) {
  const full = Math.round(rating ?? 0);
  return '★'.repeat(full) + '☆'.repeat(5 - full);
}

// ───────────── Дати ─────────────
// API віддає місцевий час майстерні рядками "2026-11-12" і "2026-11-12T09:30" без часового поясу.
// Розбираємо їх вручну, щоб браузер не зсунув дату за своїм поясом.
function parseDate(value) {
  const [y, m, d] = value.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d);
}

const timeOf = (value) => value.slice(11, 16);

const formats = {
  dayMonth: new Intl.DateTimeFormat('uk', { day: 'numeric', month: 'long' }),
  dayMonthShort: new Intl.DateTimeFormat('uk', { day: 'numeric', month: 'short' }),
  weekdayLong: new Intl.DateTimeFormat('uk', { weekday: 'long' }),
  full: new Intl.DateTimeFormat('uk', { day: 'numeric', month: 'long', year: 'numeric' }),
};

const dayMonth = (value) => formats.dayMonth.format(parseDate(value));

/** «середа, 12 листопада» */
function longDate(value) {
  const date = parseDate(value);
  return `${formats.weekdayLong.format(date)}, ${formats.dayMonth.format(date)}`;
}

/** Дата відгуку (UTC з сервера) — «12 листопада 2026». */
const reviewDate = (value) => formats.full.format(new Date(value));

/** Розклад одним рядком: «Пн–Пт 09:00–18:00 · Сб 10:00–15:00». */
function scheduleLine(schedule) {
  const groups = [];
  for (const day of schedule) {
    const last = groups.at(-1);
    if (last && last.start === day.start && last.end === day.end && day.weekday === last.to + 1) {
      last.to = day.weekday;
      last.toName = day.day;
      last.size++;
    } else {
      groups.push({ from: day.weekday, to: day.weekday, fromName: day.day, toName: day.day, start: day.start, end: day.end, size: 1 });
    }
  }
  // Сусідні дні з однаковими годинами — діапазоном, окремі — через кому
  const merged = [];
  for (const g of groups) {
    const last = merged.at(-1);
    const label = g.size >= 3 ? `${g.fromName}–${g.toName}` : g.size === 2 ? `${g.fromName}, ${g.toName}` : g.fromName;
    if (last && last.start === g.start && last.end === g.end) last.label += `, ${label}`;
    else merged.push({ label, start: g.start, end: g.end });
  }
  return merged.map((g) => `${g.label} ${g.start}–${g.end}`).join(' · ');
}

// ───────────── Іконки ─────────────
function icon(name) {
  const paths = {
    pin: '<path d="M12 21s-7-6.1-7-11.5A7 7 0 0 1 19 9.5C19 14.9 12 21 12 21z"/><circle cx="12" cy="9.5" r="2.5"/>',
    clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
    phone: '<path d="M5 4h4l2 5-2.5 1.5a11 11 0 0 0 5 5L15 13l5 2v4a2 2 0 0 1-2 2A16 16 0 0 1 3 6a2 2 0 0 1 2-2"/>',
    wrench: '<path d="M14.7 6.3a4 4 0 0 0-5.4 5.2L3 17.8V21h3.2l6.3-6.3a4 4 0 0 0 5.2-5.4l-2.6 2.6-2.4-.6-.6-2.4z"/>',
    back: '<path d="M15 18l-6-6 6-6"/>',
    chat: '<path d="M12 4H8a5 5 0 0 0-5 5v4a5 5 0 0 0 5 5h1v3l4-3h3a5 5 0 0 0 5-5V9a5 5 0 0 0-5-5z"/>',
    filter: '<path d="M4 6h16M7 12h10M10 18h4"/>',
  };
  const span = document.createElement('span');
  span.innerHTML = `<svg class="i" viewBox="0 0 24 24" aria-hidden="true">${paths[name] ?? ''}</svg>`;
  return span.firstChild;
}
