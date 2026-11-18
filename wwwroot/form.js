// Підсумок консультації (MECH-86): кнопка у верхній панелі чату відкриває форму, яку заповнює модель
// за розмовою, а користувач може виправити, зберегти й скопіювати текстом для майстра СТО.
// Використовує з chat.js: $, api(), toast(), view (відкрита розмова). chat.js підключено раніше,
// а його змінні верхнього рівня спільні для всіх звичайних (не module) скриптів сторінки.

const formDialog = $('#formDialog');
const formStatus = $('#formStatus');
const formStale = $('#formStale');
const formMessage = $('#formMessage');
const formFields = $('#formFields');
const fillButton = $('#formFill');
const saveButton = $('#formSave');
const copyButton = $('#formCopy');

const URGENCY_TEXT = {
  low: 'низька — можна планово',
  medium: 'середня — найближчим часом',
  high: 'висока — якнайшвидше',
  critical: 'критична — не їхати',
};

// Стан діалогу: для якої розмови він відкритий, що збережено на сервері, чи є незбережені правки
let formChatId = null;
let savedForm = null;
let formDirty = false;
let formBusy = false;

const field = (id) => $(`#f-${id}`);
const lines = (id) => field(id).value.split('\n').map((s) => s.trim()).filter(Boolean);
const text = (id) => field(id).value.trim() || null;
const number = (id) => {
  const value = field(id).value.trim();
  return value === '' ? null : Math.round(Number(value));
};

// Повідомлення всередині діалогу: звичайний toast() сторінки опиняється під модальним вікном і його не видно
function notify(message, isError = false) {
  formMessage.textContent = message;
  formMessage.classList.toggle('error', isError);
  formMessage.hidden = !message;
}

function writeForm(form) {
  const car = form?.car ?? {};
  field('make').value = car.make ?? '';
  field('model').value = car.model ?? '';
  field('year').value = car.year ?? '';
  field('engine').value = car.engine ?? '';
  field('fuel').value = car.fuel ?? '';
  field('mileage').value = car.mileageKm ?? '';
  field('problem').value = form?.problemSummary ?? '';
  field('symptoms').value = (form?.symptoms ?? []).join('\n');
  field('causes').value = (form?.possibleCauses ?? []).join('\n');
  field('urgency').value = form?.urgency ?? '';
  field('safe').value = form?.safeToDrive == null ? '' : String(form.safeToDrive);
  field('safetyNote').value = form?.safetyNote ?? '';
  field('actions').value = (form?.recommendedActions ?? []).join('\n');
  field('workTypes').value = (form?.workTypes ?? []).join('\n');
  field('missing').value = (form?.missingInfo ?? []).join('\n');
  formDirty = false;
}

function readForm() {
  const safe = field('safe').value;
  return {
    car: {
      make: text('make'),
      model: text('model'),
      year: number('year'),
      engine: text('engine'),
      fuel: text('fuel'),
      mileageKm: number('mileage'),
    },
    problemSummary: text('problem'),
    symptoms: lines('symptoms'),
    possibleCauses: lines('causes'),
    urgency: field('urgency').value || null,
    safeToDrive: safe === '' ? null : safe === 'true',
    safetyNote: text('safetyNote'),
    recommendedActions: lines('actions'),
    workTypes: lines('workTypes'),
    missingInfo: lines('missing'),
  };
}

function formatDate(iso) {
  return new Date(iso).toLocaleString('uk', { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' });
}

function showFormState() {
  const hasForm = Boolean(savedForm);
  formFields.hidden = !hasForm;
  saveButton.hidden = !hasForm;
  copyButton.hidden = !hasForm;
  fillButton.textContent = hasForm ? 'Оновити з розмови' : 'Заповнити з розмови';
  fillButton.classList.toggle('cf-primary', !hasForm);

  if (!hasForm) {
    formStatus.textContent = 'Підсумку ще немає. ШІ прочитає розмову й заповнить форму: авто, проблему, терміновість і що робити далі. Потім її можна виправити.';
  } else {
    const who = savedForm.editedByUser ? `Змінено вами ${formatDate(savedForm.updatedAt)}` : `Заповнено ШІ ${formatDate(savedForm.generatedAt)}`;
    formStatus.textContent = `${who}. Перевірте дані: ШІ міг щось пропустити.`;
  }

  // Підсумок застарів, якщо після нього в розмові з'явилися нові повідомлення
  const messages = view.id === formChatId ? view.chat?.messages.length : undefined;
  formStale.hidden = !hasForm || messages === undefined || messages <= savedForm.basedOnMessages;
}

function setFormBusy(busy, message) {
  formBusy = busy;
  for (const button of [fillButton, saveButton, copyButton]) button.disabled = busy;
  formDialog.querySelector('.cf').setAttribute('aria-busy', String(busy));
  if (message) formStatus.textContent = message;
}

async function openForm() {
  if (!view.id) {
    toast('Спершу опишіть проблему асистенту — тоді буде що підсумувати.');
    return;
  }
  formChatId = view.id;
  savedForm = null;
  notify('');
  writeForm(null);
  formFields.hidden = true;
  formStale.hidden = true;
  formDialog.showModal();
  setFormBusy(true, 'Завантаження…');
  try {
    const form = await api(`/chats/${formChatId}/form`); // 204 → null: ще не заповнювали
    if (formChatId !== view.id) return;
    savedForm = form;
    writeForm(form);
  } catch (error) {
    notify(error.message, true);
    return;
  } finally {
    setFormBusy(false);
  }
  showFormState();
}

async function fillForm() {
  if (formBusy) return;
  if (savedForm && (formDirty || savedForm.editedByUser) &&
      !confirm('ШІ заповнить форму заново і ваші правки буде втрачено. Продовжити?')) return;

  const chatId = formChatId;
  notify('');
  setFormBusy(true, 'ШІ читає розмову й заповнює форму…');
  try {
    const form = await api(`/chats/${chatId}/form`, { method: 'POST' });
    if (chatId !== formChatId) return;
    savedForm = form;
    writeForm(form);
  } catch (error) {
    if (chatId === formChatId) notify(error.message, true);
  } finally {
    setFormBusy(false);
    if (chatId === formChatId) showFormState();
  }
}

async function saveForm() {
  if (formBusy) return;
  const chatId = formChatId;
  notify('');
  setFormBusy(true, 'Збереження…');
  try {
    const form = await api(`/chats/${chatId}/form`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(readForm()),
    });
    if (chatId !== formChatId) return;
    savedForm = form;
    writeForm(form); // сервер міг поправити дані (обрізати довге, прибрати неможливий рік) — показуємо як збережено
    notify('Підсумок збережено.');
  } catch (error) {
    if (chatId === formChatId) notify(error.message, true);
  } finally {
    setFormBusy(false);
    if (chatId === formChatId) showFormState();
  }
}

// Текстом — щоб надіслати майстру в месенджер або пошту
function formAsText(form) {
  const car = form.car;
  const carLine = [car.make, car.model, car.year, car.engine, car.fuel, car.mileageKm != null ? `${car.mileageKm.toLocaleString('uk')} км` : null]
    .filter((x) => x != null && x !== '').join(', ');
  const list = (title, items) => (items.length ? `${title}:\n${items.map((x) => `• ${x}`).join('\n')}` : null);
  const safe = form.safeToDrive == null ? null : `Чи можна їхати: ${form.safeToDrive ? 'так' : 'ні'}${form.safetyNote ? ` — ${form.safetyNote}` : ''}`;
  return [
    'Підсумок консультації',
    carLine && `Авто: ${carLine}`,
    form.problemSummary && `Проблема: ${form.problemSummary}`,
    list('Симптоми', form.symptoms),
    list('Можливі причини', form.possibleCauses),
    form.urgency && `Терміновість: ${URGENCY_TEXT[form.urgency]}`,
    safe,
    list('Рекомендовані дії', form.recommendedActions),
    form.workTypes.length ? `Тип робіт: ${form.workTypes.join(', ')}` : null,
    list('Варто уточнити', form.missingInfo),
  ].filter(Boolean).join('\n\n');
}

async function copyForm() {
  try {
    await navigator.clipboard.writeText(formAsText(readForm()));
    notify('Підсумок скопійовано — можна надіслати майстру.');
  } catch {
    notify('Не вдалося скопіювати. Виділіть текст у полях вручну.', true);
  }
}

function closeForm() {
  if (formDirty && !confirm('Закрити без збереження змін?')) return;
  formDialog.close();
}

$('#summaryBtn').addEventListener('click', openForm);
$('#formClose').addEventListener('click', closeForm);
fillButton.addEventListener('click', fillForm);
saveButton.addEventListener('click', saveForm);
copyButton.addEventListener('click', copyForm);
formFields.addEventListener('input', () => { formDirty = true; });
formDialog.addEventListener('cancel', (event) => { // Esc
  event.preventDefault();
  closeForm();
});
formDialog.addEventListener('close', () => { formChatId = null; formDirty = false; });
// Перейшли в іншу розмову (назад/вперед у браузері) — форма стосувалась попередньої
addEventListener('hashchange', () => { if (formDialog.open) formDialog.close(); });
