// Сторінка входу і реєстрації. Сесія — HttpOnly-cookie, яку ставить сервер: тут ми її не бачимо й не зберігаємо.

const $ = (selector) => document.querySelector(selector);
const form = $('#authForm');
const title = $('#authTitle');
const email = $('#email');
const password = $('#password');
const passwordHint = $('#passwordHint');
const remember = $('#remember');
const rememberRow = $('#rememberRow');
const errorEl = $('#authError');
const submit = $('#submit');
const modeLogin = $('#modeLogin');
const modeRegister = $('#modeRegister');

let mode = new URLSearchParams(location.search).get('mode') === 'register' ? 'register' : 'login';

// Куди повернутися після входу. Лише адреси нашого ж сайту: інакше посилання на кшталт
// login.html?next=https://evil.example після входу відправило б користувача на чужий сайт
function nextUrl() {
  const next = new URLSearchParams(location.search).get('next');
  if (next && next.startsWith('/') && !next.startsWith('//') && !next.startsWith('/\\')) return next;
  return 'chat.html';
}

function setMode(next) {
  mode = next;
  const register = mode === 'register';
  title.textContent = register ? 'Створити акаунт' : 'Вхід';
  document.title = title.textContent;
  submit.textContent = register ? 'Створити акаунт' : 'Увійти';
  modeLogin.setAttribute('aria-pressed', String(!register));
  modeRegister.setAttribute('aria-pressed', String(register));
  // Підказки менеджеру паролів: новий пароль — запропонувати згенерувати, наявний — підставити
  password.autocomplete = register ? 'new-password' : 'current-password';
  password.minLength = register ? 8 : 0;
  passwordHint.hidden = !register;
  rememberRow.hidden = register;
  showError('');
}

function showError(message) {
  errorEl.textContent = message;
  errorEl.hidden = !message;
}

async function post(path, body) {
  let response;
  try {
    response = await fetch(`/api/auth/${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
  } catch {
    return { ok: false, error: 'Немає зв’язку із сервером. Перевірте інтернет і спробуйте ще раз.' };
  }
  const data = await response.json().catch(() => null);
  return { ok: response.ok, error: data?.error ?? 'Сервер відповів помилкою. Спробуйте ще раз.' };
}

form.addEventListener('submit', async (event) => {
  event.preventDefault();
  const values = { email: email.value.trim(), password: password.value };
  if (!values.email || !values.password) return showError('Вкажіть email і пароль.');
  if (mode === 'register' && values.password.length < 8) return showError('Пароль має містити щонайменше 8 символів.');

  submit.disabled = true;
  showError('');
  const result = mode === 'register'
    ? await post('register', values)
    : await post('login', { ...values, rememberMe: remember.checked });
  submit.disabled = false;

  if (result.ok) location.replace(nextUrl());
  else {
    showError(result.error);
    password.select();
  }
});

modeLogin.addEventListener('click', () => setMode('login'));
modeRegister.addEventListener('click', () => setMode('register'));

// Уже увійшли (наприклад, відкрили login.html із закладок) — одразу до чату
fetch('/api/auth/me').then((response) => { if (response.ok) location.replace(nextUrl()); }, () => {});

setMode(mode);
email.focus();
