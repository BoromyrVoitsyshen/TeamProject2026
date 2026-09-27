// Сторінка чату: панель розмов, стрічка повідомлень, поле вводу з фото.
// Із сервером сторінка спілкується лише через api() — маршрути описані в server/index.js.

// ───────────── Налаштування ─────────────
// Ліміти ті самі, що в server/config.js. Змінюєте — змініть в обох файлах.
const MAX_IMAGE_MB = 10;
const MAX_IMAGES = 10;
const MAX_TEXT = 10_000;
const IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
const DEFAULT_TITLE = 'Нова розмова';

// ───────────── Елементи сторінки ─────────────
const $ = (selector) => document.querySelector(selector);
const drawer = $('#drawer');
const chatList = $('#chatList');
const main = $('#main');
const toggleButton = $('#toggleDrawer');
const titleEl = $('#chatTitle');
const messagesEl = $('#messages');
const composer = $('#composer');
const previewsEl = $('#previews');
const fileInput = $('#fileInput');
const input = $('#input');
const sendButton = $('#send');
const dropzone = $('#dropzone');
const lightbox = $('#lightbox');
const lightboxImg = lightbox.querySelector('img');
const toastEl = $('#toast');

const wide = matchMedia('(min-width: 900px)');                        // панель стоїть поруч із чатом
const canHover = matchMedia('(hover: hover)');                        // є миша — можна ставити фокус у поле вводу
const touchFirst = matchMedia('(hover: none) and (pointer: coarse)'); // телефон — Enter переносить рядок

// ───────────── Стан ─────────────
let chats = [];                // розмови для панелі (коротко), свіжі зверху
let view = { id: undefined };  // відкрита розмова { id, chat }; id === null — нова, ще не збережена
let attachments = [];          // фото в полі вводу: { file, url }
let statusEl = null;           // «агент відповідає» або «агент не відповів» у кінці стрічки
let renaming = null;           // id розмови, яку зараз перейменовують
let stuck = true;              // стрічка внизу — нові повідомлення тримаємо на виду
const busy = new Map();        // id розмови → запит, що чекає на відповідь агента
const drafts = new Map();      // id розмови ('' — нова) → недописане повідомлення { text, attachments }
const localPhotos = new Map(); // адреса фото на сервері → локальна копія, щоб щойно надіслане не вантажилось удруге
const deleted = new Set();     // видалені розмови: запізнілі відповіді для них ігноруємо

// ───────────── Сервер ─────────────
class ApiError extends Error {
  constructor(status, message, data) {
    super(message);
    this.status = status; // 0 — немає зв'язку
    this.data = data;
  }
}

async function api(path, options = {}) {
  let response;
  try {
    response = await fetch(`/api${path}`, options);
  } catch {
    throw new ApiError(0, 'Немає зв’язку із сервером. Перевірте інтернет і спробуйте ще раз.');
  }
  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    const fallback = response.status === 413 ? 'Файли завеликі для сервера.' : 'Сервер відповів помилкою. Спробуйте ще раз.';
    throw new ApiError(response.status, data?.error ?? fallback, data);
  }
  return data;
}

// ───────────── Бічна панель ─────────────
function drawerShown() {
  return wide.matches ? !document.body.classList.contains('drawer-closed') : document.body.classList.contains('drawer-open');
}

function syncDrawer() {
  const shown = drawerShown();
  toggleButton.setAttribute('aria-expanded', String(shown));
  drawer.inert = !shown;                // схована панель не ловить фокус клавіатури
  main.inert = shown && !wide.matches;  // на телефоні відкрита панель перекриває чат
}

function setDrawer(open) {
  const hadFocus = drawer.contains(document.activeElement);
  if (wide.matches) {
    document.body.classList.toggle('drawer-closed', !open);
    try { localStorage.setItem('drawer', open ? 'open' : 'closed'); } catch {}
  } else {
    document.body.classList.toggle('drawer-open', open);
  }
  syncDrawer();
  if (open && !wide.matches) (chatList.querySelector('[aria-current]') ?? $('#newChat')).focus();
  if (!open && hadFocus) toggleButton.focus();
}

// ───────────── Список розмов ─────────────
async function loadList() {
  try {
    const fresh = await api('/chats');
    const mine = new Map(chats.map((chat) => [chat.id, chat]));
    // Поки повідомлення надсилається, наша версія розмови свіжіша за серверну
    chats = fresh
      .filter((chat) => !deleted.has(chat.id))
      .map((chat) => (busy.has(chat.id) && mine.get(chat.id)?.updatedAt > chat.updatedAt ? mine.get(chat.id) : chat));
    return true;
  } catch (error) {
    toast(error.message);
    return false;
  } finally {
    renderList();
  }
}

// Список оновлюється на місці: наявні пункти не перестворюються, тож не губиться фокус і не зриваються кліки
function renderList() {
  if (renaming) return; // домалюємо, коли закінчать перейменування
  if (!chats.length) {
    chatList.replaceChildren(h('p', { className: 'list-empty', textContent: 'Тут з’являться ваші розмови.' }));
    return;
  }
  const items = new Map([...chatList.querySelectorAll(':scope > .chat-item')].map((el) => [el.dataset.id, el]));
  chats.forEach((chat, index) => {
    const item = items.get(chat.id) ?? createItem(chat.id);
    fillItem(item, chat);
    if (chatList.children[index] !== item) chatList.insertBefore(item, chatList.children[index] ?? null);
  });
  while (chatList.children.length > chats.length) chatList.lastElementChild.remove();
}

function createItem(id) {
  const link = h('a', { className: 'chat-open', href: `#${id}` },
    h('span', { className: 'chat-name' }),
    h('time', { className: 'chat-time' }),
    h('span', { className: 'chat-preview' }));
  const actions = h('div', { className: 'chat-actions' },
    h('button', { type: 'button', className: 'icon-btn', title: 'Перейменувати', dataset: { action: 'rename' } }, icon('rename')),
    h('button', { type: 'button', className: 'icon-btn', title: 'Видалити', dataset: { action: 'delete' } }, icon('delete')));
  return h('div', { className: 'chat-item', dataset: { id } }, link, actions);
}

function fillItem(item, chat) {
  const active = chat.id === view.id;
  const link = item.querySelector('.chat-open');
  const time = item.querySelector('.chat-time');
  item.classList.toggle('active', active);
  if (active) link.setAttribute('aria-current', 'page');
  else link.removeAttribute('aria-current');
  setText(item.querySelector('.chat-name'), chat.title);
  setText(time, listTime(chat.updatedAt));
  time.dateTime = chat.updatedAt;
  setText(item.querySelector('.chat-preview'), chat.preview || 'Без повідомлень');
  item.querySelector('[data-action="rename"]').setAttribute('aria-label', `Перейменувати «${chat.title}»`);
  item.querySelector('[data-action="delete"]').setAttribute('aria-label', `Видалити «${chat.title}»`);
}

// Записати свіжі дані розмови в список (з відповіді сервера або наші власні)
function saveSummary(summary) {
  if (!summary || deleted.has(summary.id)) return;
  const index = chats.findIndex((chat) => chat.id === summary.id);
  if (index >= 0) chats[index] = { ...chats[index], ...summary };
  else if (summary.updatedAt) chats.push(summary);
  else return;
  chats.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  renderList();
  if (view.id === summary.id && summary.title) {
    setTitle(summary.title);
    if (view.chat) view.chat.title = summary.title;
  }
}

// Те саме, що summary() на сервері
function summarize(chat) {
  const last = chat.messages.at(-1);
  const preview = last ? last.text || (last.images.length ? 'Фото' : '') : '';
  return { id: chat.id, title: chat.title, createdAt: chat.createdAt, updatedAt: chat.updatedAt, preview: preview.slice(0, 120) };
}

// Те саме, що titleFrom() на сервері: назва розмови з першого повідомлення
function titleFrom({ text, images }) {
  const line = text.replace(/\s+/g, ' ').trim();
  if (!line) return images.length ? 'Фото' : DEFAULT_TITLE;
  return line.length > 48 ? `${line.slice(0, 48).trimEnd()}…` : line;
}

function startRename(item) {
  const id = item.dataset.id;
  const chat = chats.find((c) => c.id === id);
  if (!chat || renaming) return;
  renaming = id;
  const field = h('input', { className: 'rename', value: chat.title, maxLength: 100, enterKeyHint: 'done' });
  field.setAttribute('aria-label', 'Назва розмови');
  for (const child of item.children) child.hidden = true;
  item.append(field);
  field.focus();
  field.select();

  const finish = async (save) => {
    if (renaming !== id) return;
    renaming = null;
    const hadFocus = document.activeElement === field;
    const old = chats.find((c) => c.id === id)?.title ?? chat.title;
    const title = field.value.replace(/\s+/g, ' ').trim().slice(0, 100);
    field.remove();
    for (const child of item.children) child.hidden = false;
    const changed = save && title && title !== old;
    if (changed) saveSummary({ id, title });
    else renderList();
    if (hadFocus) item.querySelector('.chat-open').focus();
    if (!changed) return;
    try {
      saveSummary(await api(`/chats/${id}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ title }),
      }));
    } catch (error) {
      if (error.status === 404) return chatGone(id);
      saveSummary({ id, title: old });
      toast(error.message);
    }
  };
  field.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      finish(true);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation(); // Esc закриває лише редагування, а не панель
      finish(false);
    }
  });
  field.addEventListener('blur', () => finish(true));
}

async function removeChat(id) {
  const chat = chats.find((c) => c.id === id);
  if (!chat || !confirm(`Видалити розмову «${chat.title}»?\nФото з неї теж буде видалено.`)) return;
  try {
    await api(`/chats/${id}`, { method: 'DELETE' });
  } catch (error) {
    if (error.status !== 404) return toast(error.message);
  }
  forgetChat(id);
  if (!document.activeElement || document.activeElement === document.body) focusInput();
}

function forgetChat(id) {
  deleted.add(id);
  busy.delete(id);
  dropDraft(id);
  chats = chats.filter((chat) => chat.id !== id);
  renderList();
  if (view.id === id) {
    history.replaceState(null, '', location.pathname + location.search);
    show(null, { keepComposer: true }); // недописаний текст лишається в полі
  }
}

function chatGone(id) {
  if (deleted.has(id)) return;
  toast('Розмову не знайдено — можливо, її видалили.');
  forgetChat(id);
}

// ───────────── Відкриття розмови ─────────────
// Відкрита розмова записана в адресі (chat.html#<id>): її можна зберегти в закладки,
// а кнопки браузера «назад» і «вперед» перемикають розмови.
function route() {
  const id = decodeURIComponent(location.hash.slice(1)) || null;
  if (!wide.matches && drawerShown()) setDrawer(false);
  if (lightbox.open) lightbox.close();
  if (id !== view.id) show(id);
}

function newChat() {
  if (!wide.matches) setDrawer(false);
  if (view.id === null && !view.creating) return focusInput();
  history.pushState(null, '', location.pathname + location.search);
  show(null);
}

function show(id, { keepComposer = false } = {}) {
  if (keepComposer) dropDraft(id ?? '');
  else stashDraft();
  const current = (view = { id, chat: null });
  if (!keepComposer) loadDraft(id ?? '');
  statusEl = null;
  messagesEl.replaceChildren();
  renderList(); // підсвітити відкриту розмову
  setTitle(id ? chats.find((chat) => chat.id === id)?.title ?? '' : DEFAULT_TITLE);
  updateSend();

  if (!id) {
    messagesEl.append(emptyState());
    focusInput();
    return;
  }
  messagesEl.setAttribute('aria-busy', 'true');
  api(`/chats/${encodeURIComponent(id)}`)
    .then((chat) => {
      if (current !== view) return; // поки вантажилось, відкрили іншу розмову
      current.chat = chat;
      setTitle(chat.title);
      renderView();
      focusInput();
    })
    .catch((error) => {
      if (current !== view) return;
      if (error.status === 404) return chatGone(id);
      messagesEl.replaceChildren(h('div', { className: 'empty' },
        h('h2', { textContent: 'Не вдалося відкрити розмову' }),
        h('p', { textContent: error.message }),
        h('button', { type: 'button', className: 'pill', textContent: 'Спробувати ще раз', dataset: { action: 'reload' } })));
    })
    .finally(() => {
      if (current === view) messagesEl.removeAttribute('aria-busy');
    });
}

// Стрічка цілком: збережені повідомлення + те, що саме надсилається
function renderView() {
  const { id, chat } = view;
  const list = [...(chat?.messages ?? [])];
  const sending = busy.get(id)?.message;
  if (sending && !list.some((m) => m.id === sending.id)) list.push(sending);

  statusEl = null;
  messagesEl.replaceChildren();
  if (!list.length) messagesEl.append(emptyState());
  list.forEach(addMessage);
  updateStatus();
  scrollToBottom();
  if (chat?.busy && !busy.has(id)) watch(view);
}

// Свіжі дані відкритої розмови із сервера: дописуємо в стрічку те, чого там ще немає
function applyChat(chat) {
  view.chat = chat;
  setTitle(chat.title);
  syncMessages();
  updateStatus();
  updateSend();
  if (stuck) scrollToBottom();
  if (chat.busy && !busy.has(chat.id)) watch(view);
}

// Агент відповідає на повідомлення з іншої вкладки чи надіслане до перезавантаження сторінки.
// Перевіряємо кожні дві секунди, поки відповідь не з'явиться.
async function watch(watched) {
  if (watched.watching) return;
  watched.watching = true;
  while (watched === view && watched.chat?.busy && !busy.has(watched.id)) {
    await sleep(2000);
    if (watched !== view || busy.has(watched.id)) break;
    try {
      const chat = await api(`/chats/${watched.id}`);
      if (watched !== view) break;
      applyChat(chat);
      saveSummary(summarize(chat));
    } catch (error) {
      if (error.status === 404 && watched === view) {
        chatGone(watched.id);
        break;
      }
    }
  }
  watched.watching = false;
}

// Коли повертаємось на вкладку — підтягуємо зміни, зроблені в інших вкладках чи на іншому пристрої
async function refresh() {
  const listed = await loadList();
  const current = view;
  if (!listed || !current.id || !current.chat || busy.has(current.id)) return;
  const summary = chats.find((chat) => chat.id === current.id);
  if (!summary) return chatGone(current.id);
  if (summary.updatedAt === current.chat.updatedAt) return;
  const chat = await api(`/chats/${current.id}`).catch(() => null);
  if (chat && current === view) applyChat(chat);
}

// ───────────── Стрічка повідомлень ─────────────
function emptyState() {
  return h('div', { className: 'empty' },
    h('h2', { textContent: 'З чого почнемо?' }),
    h('p', { textContent: 'Напишіть повідомлення або додайте фото.' }));
}

function addMessage(message) {
  messagesEl.querySelector(':scope > .empty')?.remove();
  const day = dayKey(message.createdAt);
  if (lastMessageEl()?.dataset.day !== day) {
    place(h('div', { className: 'day', textContent: dayLabel(message.createdAt) }));
  }
  place(messageEl(message));
}

// Нове завжди стає перед рядком стану, щоб той лишався останнім
function place(el) {
  messagesEl.insertBefore(el, statusEl?.parentNode === messagesEl ? statusEl : null);
}

function lastMessageEl() {
  for (let el = messagesEl.lastElementChild; el; el = el.previousElementSibling) {
    if (el.classList.contains('msg')) return el;
  }
  return null;
}

function messageNode(id) {
  return [...messagesEl.querySelectorAll(':scope > .msg')].find((el) => el.dataset.id === id);
}

function messageEl(message) {
  const mine = message.role === 'user';
  const el = h('div', {
    className: `msg ${mine ? 'user' : 'assistant'}`,
    title: formats.full.format(new Date(message.createdAt)),
    dataset: { id: message.id, day: dayKey(message.createdAt) },
  }, h('span', { className: 'visually-hidden', textContent: mine ? 'Ви:' : 'Агент:' }));
  if (message.images?.length) el.append(photosEl(message.images));
  if (message.text) el.append(textEl(mine ? 'bubble' : 'reply', message.text));
  return el;
}

function photosEl(urls) {
  const box = h('div', { className: urls.length === 1 ? 'photos single' : 'photos' });
  if (urls.length > 1) box.style.setProperty('--cols', urls.length === 2 || urls.length === 4 ? 2 : 3);
  urls.forEach((url, index) => {
    const img = document.createElement('img');
    img.alt = '';
    img.loading = 'lazy';
    img.decoding = 'async';
    img.src = localPhotos.get(url) ?? url;
    const button = h('button', { type: 'button', className: 'photo' }, img);
    button.setAttribute('aria-label', urls.length > 1 ? `Відкрити фото ${index + 1} з ${urls.length}` : 'Відкрити фото');
    box.append(button);
  });
  return box;
}

// Текст як є (без HTML), посилання клікабельні
const LINK = /https?:\/\/[^\s<>"'«»]+[^\s<>"'«».,:;!?)\]}]/g;
function textEl(className, text) {
  const el = h('div', { className });
  let last = 0;
  for (const match of text.matchAll(LINK)) {
    el.append(text.slice(last, match.index), h('a', {
      href: match[0], target: '_blank', rel: 'noopener noreferrer', textContent: match[0],
    }));
    last = match.index + match[0].length;
  }
  el.append(text.slice(last));
  return el;
}

// Дописати в стрічку повідомлення відкритої розмови, яких там ще немає
function syncMessages() {
  const shown = new Set([...messagesEl.querySelectorAll(':scope > .msg')].map((el) => el.dataset.id));
  for (const message of view.chat?.messages ?? []) {
    if (!shown.has(message.id)) addMessage(message);
  }
}

function removeMessage(id) {
  const el = messageNode(id);
  if (!el) return;
  const before = el.previousElementSibling;
  el.remove();
  // Роздільник дня, під яким не лишилось повідомлень, теж прибираємо
  if (before?.classList.contains('day') && !before.nextElementSibling?.classList.contains('msg')) before.remove();
  if (!lastMessageEl()) messagesEl.prepend(emptyState());
}

function statusKind() {
  if (view.creating || busy.has(view.id) || view.chat?.busy) return 'typing';
  if (view.chat?.messages.at(-1)?.role === 'user') return 'failed';
  return null;
}

function updateStatus() {
  const kind = statusKind();
  if (statusEl?.dataset.kind === kind) return;
  statusEl?.remove();
  statusEl = null;
  if (kind === 'typing') {
    statusEl = h('div', { className: 'typing', dataset: { kind } },
      h('i'), h('i'), h('i'),
      h('span', { className: 'visually-hidden', textContent: 'Агент відповідає…' }));
  } else if (kind === 'failed') {
    statusEl = h('div', { className: 'failed', dataset: { kind } },
      h('span', { textContent: 'Агент не відповів.' }),
      h('button', { type: 'button', className: 'pill', textContent: 'Спробувати ще раз', dataset: { action: 'retry' } }));
  }
  if (statusEl) messagesEl.append(statusEl);
}

const nearBottom = () => messagesEl.scrollHeight - messagesEl.scrollTop - messagesEl.clientHeight < 120;
const scrollToBottom = () => { messagesEl.scrollTop = messagesEl.scrollHeight; };

// ───────────── Надсилання ─────────────
function isBusy() {
  return Boolean(view.creating || busy.has(view.id) || view.chat?.busy);
}

async function send() {
  const current = view;
  const text = input.value.trim();
  if ((!text && !attachments.length) || isBusy()) return;

  const draft = takeComposer();
  const message = {
    id: uuid(),
    role: 'user',
    text,
    images: draft.attachments.map((a) => a.url), // поки що локальні копії фото
    createdAt: new Date().toISOString(),
  };
  const job = { chatId: current.id, view: current, message, draft };
  const first = !current.id || current.chat?.messages.length === 0;
  if (current.id) busy.set(current.id, job);
  else current.creating = job;

  addMessage(message);
  updateStatus();
  updateSend();
  scrollToBottom();

  try {
    // Нова розмова з'являється на сервері разом із першим повідомленням
    if (!job.chatId) {
      const chat = await api('/chats', { method: 'POST' });
      job.chatId = chat.id;
      busy.set(chat.id, job);
      delete current.creating;
      if (current === view) {
        current.id = chat.id;
        current.chat = chat;
        history.replaceState(null, '', `#${chat.id}`);
      }
      saveSummary(summarize(chat));
    }
    const summary = chats.find((chat) => chat.id === job.chatId);
    if (summary) {
      saveSummary({
        ...summary,
        title: first && summary.title === DEFAULT_TITLE ? titleFrom(message) : summary.title,
        preview: (text || 'Фото').slice(0, 120),
        updatedAt: message.createdAt,
      });
    }

    const form = new FormData();
    form.append('id', message.id);
    form.append('text', text);
    for (const { file } of draft.attachments) form.append('images', file, file.name || 'photo');
    const data = await api(`/chats/${job.chatId}/messages`, { method: 'POST', body: form });
    rememberPhotos(data.userMessage, message);
    finishJob(job, [data.userMessage, data.assistantMessage], data.chat);
  } catch (error) {
    if (error.data?.userMessage) {
      // Повідомлення збережене, але агент не відповів — у стрічці з'явиться «Спробувати ще раз»
      rememberPhotos(error.data.userMessage, message);
      finishJob(job, [error.data.userMessage], error.data.chat);
      const title = chats.find((chat) => chat.id === job.chatId)?.title;
      if (title && view.id !== job.chatId) toast(`Агент не відповів у розмові «${title}».`);
    } else {
      await sendFailed(job, error);
    }
  } finally {
    release(job);
    updateSend();
  }
}

function release(job) {
  if (busy.get(job.chatId) === job) busy.delete(job.chatId);
  if (job.view.creating === job) delete job.view.creating;
}

// Запит завершився: дописуємо нові повідомлення, якщо розмова відкрита
function finishJob(job, messages, summary) {
  release(job);
  saveSummary(summary);
  if (view.id !== job.chatId) return;
  if (view.chat) {
    // Якщо сервер дав повідомленню інший id, перепишемо його, щоб не показати двічі
    if (job.message && messages[0].id !== job.message.id) {
      const el = messageNode(job.message.id);
      if (el) el.dataset.id = messages[0].id;
    }
    for (const message of messages) {
      if (!view.chat.messages.some((m) => m.id === message.id)) view.chat.messages.push(message);
    }
    view.chat.busy = false;
    if (summary) view.chat.updatedAt = summary.updatedAt;
    syncMessages();
  }
  updateStatus();
  updateSend();
  if (stuck) scrollToBottom();
}

async function sendFailed(job, error) {
  release(job);
  const { chatId, message, draft } = job;
  // Зв'язок міг обірватися вже після того, як сервер зберіг повідомлення, — перевіримо,
  // щоб не повернути в поле вводу те, що насправді надіслано
  if (chatId && (error.status === 0 || error.status >= 500)) {
    const chat = await api(`/chats/${chatId}`).catch(() => null);
    const saved = chat?.messages.find((m) => m.id === message.id);
    if (saved) {
      rememberPhotos(saved, message);
      saveSummary(summarize(chat));
      if (view.id === chatId) applyChat(chat);
      return;
    }
  }

  removeMessage(message.id);
  updateStatus();
  if (chatId && deleted.has(chatId)) return; // розмову щойно видалили самі

  // Повідомлення не дійшло — повертаємо його в поле вводу (або в чернетку цієї розмови)
  const gone = error.status === 404;
  restoreDraft(gone && view.id !== chatId ? '' : chatId ?? '', draft);
  if (gone) return chatGone(chatId);
  toast(`Не надіслано. ${error.message}`);
  loadList(); // прибрати з панелі наш оптимістичний підпис
  if (error.status === 409 && view.id === chatId) {
    api(`/chats/${chatId}`).then((chat) => view.id === chatId && applyChat(chat), () => {});
  }
}

// Щойно надіслані фото показуємо з локальних копій — без повторного завантаження
function rememberPhotos(saved, local) {
  saved.images.forEach((url, index) => {
    if (local.images[index]) localPhotos.set(url, local.images[index]);
  });
}

// Попросити агента відповісти ще раз
async function retry() {
  const current = view;
  const id = current.id;
  if (!id || isBusy()) return;
  if (document.activeElement?.dataset.action === 'retry') focusInput();
  const job = { chatId: id, view: current };
  busy.set(id, job);
  updateStatus();
  updateSend();
  try {
    const data = await api(`/chats/${id}/reply`, { method: 'POST' });
    finishJob(job, [data.assistantMessage], data.chat);
  } catch (error) {
    release(job);
    if (error.status === 404) return chatGone(id);
    toast(error.status === 502 ? 'Агент знову не відповів. Спробуйте трохи пізніше.' : error.message);
    if (view === current) updateStatus();
    // 409: агент уже відповів або ще відповідає з іншої вкладки — беремо свіжі дані
    if (error.status === 409) api(`/chats/${id}`).then((chat) => view === current && applyChat(chat), () => {});
  } finally {
    release(job);
    updateSend();
  }
}

// ───────────── Поле вводу і фото ─────────────
function updateSend() {
  sendButton.disabled = !(input.value.trim() || attachments.length) || isBusy();
}

function autosize() {
  input.style.height = 'auto';
  input.style.height = `${Math.min(input.scrollHeight, 200)}px`;
}

function setComposer(draft) {
  input.value = draft?.text ?? '';
  attachments = draft?.attachments ?? [];
  renderPreviews();
  autosize();
  updateSend();
}

function takeComposer() {
  const draft = { text: input.value, attachments };
  setComposer(null);
  return draft;
}

// У кожної розмови своя чернетка: перейшли в іншу й повернулись — текст і фото на місці
function stashDraft() {
  const key = view.id ?? '';
  if (input.value.trim() || attachments.length) drafts.set(key, { text: input.value, attachments });
  else drafts.delete(key);
}

function loadDraft(key) {
  setComposer(drafts.get(key));
  drafts.delete(key);
}

function dropDraft(key) {
  drafts.get(key)?.attachments.forEach((a) => URL.revokeObjectURL(a.url));
  drafts.delete(key);
}

function restoreDraft(key, draft) {
  const open = key === (view.id ?? '');
  const merged = mergeDrafts(draft, open ? { text: input.value, attachments } : drafts.get(key));
  if (open) setComposer(merged);
  else drafts.set(key, merged);
}

function mergeDrafts(a, b) {
  if (!b || (!b.text.trim() && !b.attachments.length)) return a;
  const all = [...a.attachments, ...b.attachments];
  all.slice(MAX_IMAGES).forEach((x) => URL.revokeObjectURL(x.url));
  return {
    text: [a.text, b.text].filter((t) => t.trim()).join('\n').slice(0, MAX_TEXT),
    attachments: all.slice(0, MAX_IMAGES),
  };
}

function addFiles(files) {
  const problems = new Set();
  for (const file of files) {
    if (!IMAGE_TYPES.includes(file.type)) problems.add('Можна додати лише фото: JPG, PNG, WEBP або GIF.');
    else if (file.size > MAX_IMAGE_MB * 1024 * 1024) problems.add(`Фото має бути не більше ${MAX_IMAGE_MB} МБ.`);
    else if (attachments.length >= MAX_IMAGES) problems.add(`В одне повідомлення можна додати до ${MAX_IMAGES} фото.`);
    else attachments.push({ file, url: URL.createObjectURL(file) });
  }
  if (problems.size) toast([...problems].join(' '));
  renderPreviews();
  updateSend();
  focusInput();
}

function renderPreviews() {
  previewsEl.replaceChildren(...attachments.map((a, index) => {
    const remove = h('button', { type: 'button', dataset: { index } }, icon('close'));
    remove.setAttribute('aria-label', `Прибрати фото ${index + 1}`);
    return h('div', { className: 'preview' }, h('img', { src: a.url, alt: '' }), remove);
  }));
  previewsEl.hidden = !attachments.length;
}

// ───────────── Перегляд фото ─────────────
function openPhoto(src) {
  lightboxImg.src = src;
  lightbox.showModal();
}

// ───────────── Дрібниці ─────────────
function h(tag, props = {}, ...children) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(props)) {
    if (key === 'dataset') Object.assign(el.dataset, value);
    else el[key] = value;
  }
  el.append(...children);
  return el;
}

const setText = (el, text) => {
  if (el.textContent !== text) el.textContent = text;
};

const ICONS = {
  rename: '<path d="M4 20h4L19 9a2.8 2.8 0 0 0-4-4L4 16z"/><path d="m13.5 6.5 4 4"/>',
  delete: '<path d="M4 7h16M10 11v6M14 11v6M9 7V4h6v3"/><path d="m6 7 1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12"/>',
  close: '<path d="M18 6 6 18M6 6l12 12"/>',
};

function icon(name) {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('class', 'i');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('aria-hidden', 'true');
  svg.innerHTML = ICONS[name];
  return svg;
}

function setTitle(title) {
  titleEl.textContent = title;
  document.title = title || 'Чат';
}

// На телефоні фокус у полі відкриває клавіатуру, тож ставимо його лише там, де є миша
function focusInput() {
  if (canHover.matches && !main.inert) input.focus({ preventScroll: true });
}

let toastTimer;
function toast(text) {
  toastEl.textContent = text;
  toastEl.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toastEl.classList.remove('show'), 4500);
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// crypto.randomUUID працює лише на https і localhost; для відкриття за IP у локальній мережі — запасний варіант
function uuid() {
  if (crypto.randomUUID) return crypto.randomUUID();
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

// Дати: «14:05», «вчора», «пн», «12 вер.» — у списку; «Сьогодні», «12 вересня» — у стрічці
const formats = {
  time: new Intl.DateTimeFormat('uk', { hour: '2-digit', minute: '2-digit' }),
  weekday: new Intl.DateTimeFormat('uk', { weekday: 'short' }),
  dayMonth: new Intl.DateTimeFormat('uk', { day: 'numeric', month: 'short' }),
  numeric: new Intl.DateTimeFormat('uk', { day: '2-digit', month: '2-digit', year: '2-digit' }),
  day: new Intl.DateTimeFormat('uk', { day: 'numeric', month: 'long' }),
  dayYear: new Intl.DateTimeFormat('uk', { day: 'numeric', month: 'long', year: 'numeric' }),
  full: new Intl.DateTimeFormat('uk', { dateStyle: 'long', timeStyle: 'short' }),
};

function daysAgo(date) {
  const day = new Date(date).setHours(0, 0, 0, 0);
  const today = new Date().setHours(0, 0, 0, 0);
  return Math.round((today - day) / 86_400_000);
}

const thisYear = (date) => date.getFullYear() === new Date().getFullYear();

function listTime(value) {
  const date = new Date(value);
  const days = daysAgo(date);
  if (days < 1) return formats.time.format(date);
  if (days === 1) return 'вчора';
  if (days < 7) return formats.weekday.format(date);
  return (thisYear(date) ? formats.dayMonth : formats.numeric).format(date);
}

function dayLabel(value) {
  const date = new Date(value);
  const days = daysAgo(date);
  if (days < 1) return 'Сьогодні';
  if (days === 1) return 'Вчора';
  return (thisYear(date) ? formats.day : formats.dayYear).format(date);
}

function dayKey(value) {
  const date = new Date(value);
  return `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
}

// ───────────── Події ─────────────
toggleButton.addEventListener('click', () => setDrawer(!drawerShown()));
$('#backdrop').addEventListener('click', () => setDrawer(false));
$('#newChat').addEventListener('click', newChat);
$('#newChatTop').addEventListener('click', newChat);
wide.addEventListener('change', () => {
  document.body.classList.remove('drawer-open');
  syncDrawer();
});
addEventListener('keydown', (event) => {
  if (event.key === 'Escape' && !wide.matches && drawerShown()) setDrawer(false);
});

chatList.addEventListener('click', (event) => {
  const item = event.target.closest('.chat-item');
  if (!item) return;
  const action = event.target.closest('[data-action]')?.dataset.action;
  if (action === 'rename') startRename(item);
  else if (action === 'delete') removeChat(item.dataset.id);
  else if (item.dataset.id === view.id && !wide.matches) setDrawer(false); // інші закриє route() після переходу
});

addEventListener('popstate', route);
addEventListener('hashchange', route);
document.addEventListener('visibilitychange', () => {
  if (!document.hidden) refresh();
});
addEventListener('pageshow', (event) => {
  if (event.persisted) refresh();
});

messagesEl.addEventListener('click', (event) => {
  const photo = event.target.closest('.photo');
  if (photo) return openPhoto(photo.querySelector('img').src);
  const action = event.target.closest('[data-action]')?.dataset.action;
  if (action === 'retry') retry();
  else if (action === 'reload') show(view.id);
});

// Тримаємо стрічку внизу, коли вона стає нижчою (росте поле вводу, відкривається клавіатура)
// або коли довантажується шрифт і текст переверстується
messagesEl.addEventListener('scroll', () => { stuck = nearBottom(); }, { passive: true });
new ResizeObserver(() => stuck && scrollToBottom()).observe(messagesEl);
document.fonts?.addEventListener('loadingdone', () => stuck && scrollToBottom());

composer.addEventListener('submit', (event) => {
  event.preventDefault();
  send();
});

input.addEventListener('keydown', (event) => {
  if (event.key !== 'Enter' || event.isComposing || event.keyCode === 229) return;
  // Enter — надіслати, Shift+Enter — новий рядок. На телефоні Enter переносить рядок, надсилає кнопка.
  const sends = event.ctrlKey || event.metaKey || (!event.shiftKey && !event.altKey && !touchFirst.matches);
  if (!sends) return;
  event.preventDefault();
  send();
});

input.addEventListener('input', () => {
  autosize();
  updateSend();
  if (input.value.length >= MAX_TEXT) toast(`Максимум — ${MAX_TEXT.toLocaleString('uk')} символів.`);
});

$('#attach').addEventListener('click', () => fileInput.click());
fileInput.addEventListener('change', () => {
  addFiles(fileInput.files);
  fileInput.value = ''; // щоб те саме фото можна було вибрати ще раз
});

previewsEl.addEventListener('click', (event) => {
  const button = event.target.closest('button[data-index]');
  if (!button) return;
  const index = Number(button.dataset.index);
  const [removed] = attachments.splice(index, 1);
  URL.revokeObjectURL(removed.url);
  renderPreviews();
  updateSend();
  const next = previewsEl.querySelectorAll('button')[Math.min(index, attachments.length - 1)];
  if (next) next.focus();
  else focusInput();
});

// Фото з буфера обміну (Ctrl+V)
document.addEventListener('paste', (event) => {
  if (event.target instanceof Element && event.target.closest('.rename')) return;
  const files = [...(event.clipboardData?.files ?? [])];
  if (!files.length) return;
  event.preventDefault();
  addFiles(files);
});

// Перетягування фото у вікно. Без preventDefault браузер відкрив би файл замість сторінки.
let dragDepth = 0;
let dragTimer;
const hasFiles = (event) => [...(event.dataTransfer?.types ?? [])].includes('Files');
const hideDropzone = () => {
  dragDepth = 0;
  dropzone.hidden = true;
};
addEventListener('dragenter', (event) => {
  if (!hasFiles(event)) return;
  event.preventDefault();
  dragDepth += 1;
  dropzone.hidden = false;
});
addEventListener('dragleave', (event) => {
  if (!hasFiles(event)) return;
  dragDepth -= 1;
  if (dragDepth <= 0) hideDropzone();
});
addEventListener('dragover', (event) => {
  if (!hasFiles(event)) return;
  event.preventDefault();
  event.dataTransfer.dropEffect = 'copy';
  dropzone.hidden = false;
  clearTimeout(dragTimer);
  dragTimer = setTimeout(hideDropzone, 1000); // запобіжник, якщо браузер не надішле dragleave
});
addEventListener('drop', (event) => {
  if (!hasFiles(event)) return;
  event.preventDefault();
  clearTimeout(dragTimer);
  hideDropzone();
  addFiles(event.dataTransfer.files);
});

// Перегляд фото закривається кліком повз фото, хрестиком або Esc
lightbox.addEventListener('click', (event) => {
  if (event.target !== lightboxImg) lightbox.close();
});
lightbox.addEventListener('close', () => lightboxImg.removeAttribute('src'));

// ───────────── Запуск ─────────────
input.maxLength = MAX_TEXT;
syncDrawer();
loadList().then(route);
