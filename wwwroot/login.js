// Вхід і реєстрація. Після успіху повертаємо туди, звідки прийшли (?next=…),
// а нового майстра — у кабінет, щоб він заповнив профіль і розклад.
'use strict';

const params = new URLSearchParams(location.search);
const forms = { login: $('#loginForm'), register: $('#registerForm') };
const tabs = { login: $('#tabLogin'), register: $('#tabRegister') };

init();

async function init() {
  const user = await loadMe();
  if (user) {
    location.replace(safeNext() ?? 'account.html');
    return;
  }

  show(params.get('mode') === 'register' ? 'register' : 'login', false);
  tabs.login.addEventListener('click', () => show('login'));
  tabs.register.addEventListener('click', () => show('register'));
  document.querySelectorAll('[data-switch]').forEach((link) => link.addEventListener('click', (event) => {
    event.preventDefault();
    show(link.dataset.switch);
  }));

  forms.login.addEventListener('submit', (event) => submit(event, '/auth/login', {
    email: forms.login.elements.email.value,
    password: forms.login.elements.password.value,
  }));

  forms.register.addEventListener('submit', (event) => submit(event, '/auth/register', {
    role: forms.register.elements.role.value,
    name: forms.register.elements.name.value,
    email: forms.register.elements.email.value,
    phone: forms.register.elements.phone.value,
    password: forms.register.elements.password.value,
  }));
}

function show(mode, focus = true) {
  for (const key of Object.keys(forms)) {
    forms[key].hidden = key !== mode;
    tabs[key].setAttribute('aria-selected', String(key === mode));
  }
  document.title = mode === 'register' ? 'Реєстрація — Mechs' : 'Вхід — Mechs';
  const url = new URL(location.href);
  if (mode === 'register') url.searchParams.set('mode', 'register');
  else url.searchParams.delete('mode');
  history.replaceState(null, '', url);
  if (focus) forms[mode].querySelector('input:not([type="radio"])')?.focus();
}

async function submit(event, path, body) {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const error = form.querySelector('.form-error');
  error.hidden = true;
  button.disabled = true;

  try {
    const { user } = await api(path, { method: 'POST', body });
    const isNewMaster = path.endsWith('register') && user.role === 'master';
    location.replace(isNewMaster ? 'account.html' : safeNext() ?? (user.role === 'master' ? 'account.html' : './'));
  } catch (err) {
    error.textContent = err.message;
    error.hidden = false;
    button.disabled = false;
  }
}

// Повертаємо лише на сторінки цього сайту: ?next=https://чужий-сайт не спрацює
function safeNext() {
  const next = params.get('next');
  if (!next) return null;
  try {
    const url = new URL(next, location.origin + '/');
    return url.origin === location.origin ? url.pathname + url.search + url.hash : null;
  } catch {
    return null;
  }
}
