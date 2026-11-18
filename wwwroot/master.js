// Сторінка майстра («Дізнатися більше»): повна інформація, послуги, розклад, запис на вільний час і відгуки.
'use strict';

const params = new URLSearchParams(location.search);
const masterId = Number(params.get('id'));
const page = $('#page');

let master = null;
const booking = { serviceId: null, date: null, time: null, days: [], requestNo: 0 };

init();

async function init() {
  keepCatalogFilters();
  const [user] = await Promise.all([loadMe(), loadMaster()]);
  if (!master) return;
  render(user);
  if (location.hash) document.getElementById(location.hash.slice(1))?.scrollIntoView();
}

// «← Усі майстри» повертає до каталогу з тими самими фільтрами, якщо прийшли звідти
function keepCatalogFilters() {
  try {
    const from = new URL(document.referrer);
    if (from.origin === location.origin && (from.pathname === '/' || from.pathname.endsWith('/index.html'))) {
      $('#backLink').href = from.pathname + from.search;
    }
  } catch { /* прямий перехід — лишаємо посилання на каталог */ }
}

async function loadMaster() {
  if (!Number.isInteger(masterId) || masterId <= 0) return showMissing('Майстра не знайдено.');
  try {
    master = await api(`/masters/${masterId}`);
    document.title = `${master.name} — Mechs`;
  } catch (error) {
    showMissing(error.status === 404 ? 'Майстра не знайдено. Можливо, профіль сховали.' : error.message);
  }
}

function showMissing(text) {
  $('#loading').replaceWith(h('div', { className: 'empty-state' },
    h('p', {}, text),
    h('a', { className: 'btn btn-quiet', href: './' }, 'До всіх майстрів')));
}

// ───────────── Сторінка ─────────────
function render(user) {
  const blocks = [];

  if (!master.isListed) {
    blocks.push(h('p', { className: 'notice' },
      'Профіль не опубліковано — клієнти його не бачать. ',
      h('a', { href: 'account.html' }, 'Заповнити й опублікувати')));
  }

  // Шапка профілю, картка запису, решта. На телефоні вони йдуть саме так — запис одразу під шапкою;
  // на широкому екрані картка запису стоїть праворуч і не прокручується
  blocks.push(h('div', { className: 'master-layout' },
    profileHeader(),
    h('aside', { className: 'master-aside' }, bookingCard(user)),
    h('div', { className: 'master-main' },
      master.description ? h('section', { className: 'panel' }, h('h2', {}, 'Про майстра'), h('p', { className: 'about' }, master.description)) : null,
      servicesSection(),
      scheduleSection(),
      reviewsSection(user))));

  $('#loading').replaceWith(...blocks);
  loadSlots();
}

function profileHeader() {
  const rating = master.reviewCount
    ? h('a', { className: 'rating', href: '#reviews' },
      h('span', { className: 'stars', ariaHidden: 'true' }, stars(master.rating)),
      h('strong', {}, master.rating.toLocaleString('uk')),
      h('span', { className: 'muted' }, ` · ${count(master.reviewCount, 'відгук', 'відгуки', 'відгуків')}`))
    : h('span', { className: 'rating muted' }, 'Ще без відгуків');

  return h('section', { className: 'profile-head' },
    h('div', { className: 'avatar avatar-lg', ariaHidden: 'true' }, initials(master.name)),
    h('div', {},
      h('h1', {}, master.name),
      h('p', { className: 'mc-meta' }, rating, h('span', { className: 'dot', ariaHidden: 'true' }, '·'),
        h('span', {}, `Стаж ${count(master.experienceYears, 'рік', 'роки', 'років')}`)),
      h('p', { className: 'mc-line' }, icon('pin'), h('span', {}, `${master.city}, ${master.address}`)),
      master.phone ? h('p', { className: 'mc-line' }, icon('phone'),
        h('a', { href: `tel:${master.phone.replace(/[^\d+]/g, '')}` }, master.phone)) : null));
}

function servicesSection() {
  return h('section', { className: 'panel', id: 'services' },
    h('h2', {}, 'Послуги та ціни'),
    h('ul', { className: 'service-list' }, master.services.map((s) => {
      const pick = h('button', { className: 'btn btn-quiet btn-sm', type: 'button' }, 'Записатися');
      pick.addEventListener('click', () => {
        selectService(s.serviceId);
        $('#booking').scrollIntoView({ behavior: 'smooth', block: 'start' });
      });
      return h('li', {},
        h('div', {}, h('span', { className: 'service-name' }, s.name), h('span', { className: 'muted' }, duration(s.durationMinutes))),
        h('strong', { className: 'service-price' }, money(s.price)),
        pick);
    })));
}

function scheduleSection() {
  const byDay = new Map(master.schedule.map((d) => [d.weekday, d]));
  const names = ['Понеділок', 'Вівторок', 'Середа', 'Четвер', 'Пʼятниця', 'Субота', 'Неділя'];
  return h('section', { className: 'panel' },
    h('h2', {}, 'Розклад'),
    h('dl', { className: 'week' }, names.map((name, i) => {
      const day = byDay.get(i + 1);
      return h('div', { className: day ? 'week-day' : 'week-day off' },
        h('dt', {}, name),
        h('dd', {}, day ? `${day.start}–${day.end}` : 'вихідний'));
    })),
    h('p', { className: 'muted small' },
      `Записатися можна на ${count(master.bookingHorizonDays, 'день', 'дні', 'днів')} уперед. Вільний час рахується з цього розкладу і вже зроблених записів.`));
}

// ───────────── Запис ─────────────
function bookingCard(user) {
  const viewer = master.viewer;
  const select = h('select', { id: 'bService' },
    master.services.map((s) => h('option', { value: s.serviceId }, `${s.name} — ${money(s.price)}`)));
  const preferred = Number(params.get('service'));
  booking.serviceId = master.services.some((s) => s.serviceId === preferred) ? preferred : master.services[0]?.serviceId;
  select.value = booking.serviceId;
  select.addEventListener('change', () => selectService(Number(select.value)));

  let action;
  if (viewer.isOwner) {
    action = h('p', { className: 'muted small' }, 'Це ваш профіль — так його бачать клієнти. ', h('a', { href: 'account.html' }, 'Перейти в кабінет'));
  } else if (viewer.role === 'master') {
    action = h('p', { className: 'muted small' }, 'Записуватися можуть лише клієнти. Ви увійшли як майстер.');
  } else if (!user) {
    const next = encodeURIComponent(`master.html?id=${masterId}`);
    action = h('a', { className: 'btn btn-primary btn-block', id: 'bLogin', href: `login.html?next=${next}` }, 'Увійдіть, щоб записатися');
  } else {
    action = h('button', { className: 'btn btn-primary btn-block', id: 'bSubmit', type: 'submit', disabled: true }, 'Оберіть час');
  }

  const form = h('form', { className: 'booking-card', id: 'booking', ariaLabel: 'Запис до майстра' },
    h('h2', {}, 'Записатися'),
    h('div', { className: 'field' }, h('label', { for: 'bService' }, 'Послуга'), select),
    h('p', { className: 'muted small', id: 'bDuration' }),
    h('div', { className: 'field' },
      h('span', { className: 'label', id: 'bDateLabel' }, 'День'),
      h('div', { className: 'date-strip', id: 'bDates', role: 'radiogroup', ariaLabelledby: 'bDateLabel' })),
    h('div', { className: 'field' },
      h('span', { className: 'label', id: 'bTimeLabel' }, 'Час'),
      h('div', { className: 'slots', id: 'bTimes', role: 'radiogroup', ariaLabelledby: 'bTimeLabel' })),
    h('div', { className: 'field' },
      h('label', { for: 'bComment' }, 'Що з авто? ', h('span', { className: 'muted' }, '(необов’язково)')),
      h('textarea', { id: 'bComment', rows: 3, maxLength: 500, placeholder: 'Напр. стукає спереду на ямах' })),
    h('p', { className: 'booking-summary', id: 'bSummary', ariaLive: 'polite' }),
    action);

  form.addEventListener('submit', submitBooking);
  return form;
}

function selectService(serviceId) {
  booking.serviceId = serviceId;
  const select = $('#bService');
  if (select) select.value = serviceId;
  booking.time = null;
  loadSlots();
}

async function loadSlots() {
  const offer = master.services.find((s) => s.serviceId === booking.serviceId);
  if (!offer) return;
  $('#bDuration').textContent = `Триває ${duration(offer.durationMinutes)} · ${money(offer.price)}`;
  $('#bDates').replaceChildren(h('span', { className: 'muted small' }, 'Шукаємо вільний час…'));
  $('#bTimes').replaceChildren();

  const no = ++booking.requestNo;
  try {
    const data = await api(`/masters/${masterId}/slots?serviceId=${booking.serviceId}&days=${master.bookingHorizonDays}`);
    if (no !== booking.requestNo) return;
    booking.days = data.days;
    const stillFree = booking.days.find((d) => d.date === booking.date && d.slots.length);
    booking.date = (stillFree ?? booking.days.find((d) => d.slots.length))?.date ?? null;
    if (!booking.days.find((d) => d.date === booking.date)?.slots.includes(booking.time)) booking.time = null;
    renderDates();
    renderTimes();
  } catch (error) {
    if (no !== booking.requestNo) return;
    $('#bDates').replaceChildren(h('span', { className: 'muted small' }, error.message));
  }
}

function renderDates() {
  const strip = $('#bDates');
  if (!booking.days.some((d) => d.slots.length)) {
    strip.replaceChildren(h('span', { className: 'muted small' },
      `На найближчі ${count(booking.days.length, 'день', 'дні', 'днів')} вільного часу немає.`));
    return;
  }
  strip.replaceChildren(...booking.days.map((day) => {
    const date = parseDate(day.date);
    const free = day.slots.length > 0;
    const button = h('button', {
      type: 'button',
      className: 'date-chip',
      role: 'radio',
      ariaChecked: String(day.date === booking.date),
      disabled: !free,
      title: free ? `${longDate(day.date)}: ${count(day.slots.length, 'вільне вікно', 'вільні вікна', 'вільних вікон')}` : `${longDate(day.date)}: ${day.isWorking ? 'усе зайнято' : 'вихідний'}`,
    },
    h('span', { className: 'date-wd' }, day.weekday),
    h('span', { className: 'date-d' }, date.getDate()),
    h('span', { className: 'date-m' }, formats.dayMonthShort.format(date).replace(/^\d+\s*/, '')));
    button.addEventListener('click', () => {
      booking.date = day.date;
      booking.time = null;
      renderDates();
      renderTimes();
    });
    return button;
  }));
  strip.querySelector('[aria-checked="true"]')?.scrollIntoView({ block: 'nearest', inline: 'center' });
}

function renderTimes() {
  const box = $('#bTimes');
  const day = booking.days.find((d) => d.date === booking.date);
  if (!day) {
    box.replaceChildren();
  } else {
    box.replaceChildren(...day.slots.map((time) => {
      const button = h('button', { type: 'button', className: 'slot', role: 'radio', ariaChecked: String(time === booking.time) }, time);
      button.addEventListener('click', () => {
        booking.time = time;
        renderTimes();
      });
      return button;
    }));
  }
  renderSummary();
}

function renderSummary() {
  const offer = master.services.find((s) => s.serviceId === booking.serviceId);
  const ready = Boolean(offer && booking.date && booking.time);
  $('#bSummary').textContent = ready
    ? `${offer.name}: ${longDate(booking.date)}, ${booking.time} · ${money(offer.price)}`
    : '';
  const submit = $('#bSubmit');
  if (submit) {
    submit.disabled = !ready;
    submit.textContent = ready ? `Записатися на ${dayMonth(booking.date)}, ${booking.time}` : 'Оберіть час';
  }
}

async function submitBooking(event) {
  event.preventDefault();
  const submit = $('#bSubmit');
  if (!submit || submit.disabled) return;
  submit.disabled = true;
  submit.textContent = 'Записуємо…';

  try {
    const { booking: made } = await api('/bookings', {
      method: 'POST',
      body: {
        masterId,
        serviceId: booking.serviceId,
        date: booking.date,
        time: booking.time,
        comment: $('#bComment').value,
      },
    });
    showConfirmation(made);
  } catch (error) {
    toast(error.message);
    if (error.status === 401) {
      location.href = `login.html?next=${encodeURIComponent(`master.html?id=${masterId}`)}`;
      return;
    }
    if (error.status === 409) booking.time = null; // час зайняли — показуємо свіжий графік
    await loadSlots();
  }
}

function showConfirmation(made) {
  const again = h('button', { className: 'btn btn-quiet btn-block', type: 'button' }, 'Записатися ще');
  const card = h('div', { className: 'booking-card booking-done', id: 'booking', tabIndex: -1 },
    h('p', { className: 'done-mark', ariaHidden: 'true' }, '✓'),
    h('h2', {}, 'Ви записані'),
    h('p', {}, h('strong', {}, `${longDate(made.startsAt)}, ${timeOf(made.startsAt)}`)),
    h('p', {}, `${made.service} · ${money(made.price)}`),
    h('p', { className: 'muted small' }, `${made.master.city}, ${made.master.address}`),
    h('a', { className: 'btn btn-primary btn-block', href: 'account.html' }, 'Мої записи'),
    again);
  again.addEventListener('click', () => {
    const fresh = bookingCard(currentUser);
    card.replaceWith(fresh);
    booking.time = null;
    loadSlots();
  });
  $('#booking').replaceWith(card);
  card.focus();
}

// ───────────── Відгуки ─────────────
function reviewsSection() {
  const section = h('section', { className: 'panel', id: 'reviews' });
  fillReviews(section);
  return section;
}

function fillReviews(section) {
  const total = master.reviewCount;
  const children = [h('h2', {}, total ? `Відгуки · ${total}` : 'Відгуки')];

  if (total) {
    const max = Math.max(...master.ratingBreakdown.map((r) => r.count), 1);
    children.push(h('div', { className: 'review-summary' },
      h('div', { className: 'review-score' },
        h('strong', {}, master.rating.toLocaleString('uk')),
        h('span', { className: 'stars', ariaHidden: 'true' }, stars(master.rating)),
        h('span', { className: 'muted small' }, count(total, 'відгук', 'відгуки', 'відгуків'))),
      h('ul', { className: 'review-bars', ariaLabel: 'Розподіл оцінок' }, master.ratingBreakdown.map((r) =>
        h('li', {},
          h('span', {}, `${r.stars} ★`),
          h('span', { className: 'bar' }, h('span', { style: `width:${(r.count / max) * 100}%` })),
          h('span', { className: 'muted' }, r.count))))));
  }

  if (master.viewer.canReview) children.push(reviewForm(section));
  else if (master.viewer.reviewHint) children.push(h('p', { className: 'muted small' }, master.viewer.reviewHint));

  children.push(total
    ? h('ol', { className: 'review-list' }, master.reviews.map((r) =>
      h('li', { className: 'review' },
        h('p', { className: 'review-head' },
          h('strong', {}, r.author),
          h('span', { className: 'stars', ariaLabel: `Оцінка ${r.rating} з 5`, role: 'img' }, stars(r.rating)),
          h('span', { className: 'muted small' }, reviewDate(r.createdAt))),
        r.text ? h('p', {}, r.text) : null)))
    : h('p', { className: 'muted' }, 'Ще ніхто не залишив відгук.'));

  section.replaceChildren(...children);
}

function reviewForm(section) {
  const starInputs = [5, 4, 3, 2, 1].map((n) => [
    h('input', { type: 'radio', name: 'rating', id: `star${n}`, value: n, required: true }),
    h('label', { for: `star${n}`, title: count(n, 'зірка', 'зірки', 'зірок') }, '★'),
  ]);
  const text = h('textarea', { id: 'reviewText', rows: 3, maxLength: 1000, placeholder: 'Що сподобалось, що ні?' });
  const submit = h('button', { className: 'btn btn-primary', type: 'submit' }, 'Надіслати відгук');

  const form = h('form', { className: 'review-form', id: 'review' },
    h('h3', {}, 'Ваш відгук'),
    h('fieldset', { className: 'star-input' }, h('legend', { className: 'visually-hidden' }, 'Оцінка'), starInputs),
    h('div', { className: 'field' }, h('label', { className: 'visually-hidden', for: 'reviewText' }, 'Текст відгуку'), text),
    submit);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    const rating = Number(form.elements.rating.value);
    if (!rating) return toast('Оберіть оцінку від 1 до 5 зірок.');
    submit.disabled = true;
    try {
      await api(`/masters/${masterId}/reviews`, { method: 'POST', body: { rating, text: text.value } });
      master = await api(`/masters/${masterId}`);
      fillReviews(section);
      document.querySelector('.profile-head')?.replaceWith(profileHeader());
      toast('Дякуємо за відгук!');
    } catch (error) {
      submit.disabled = false;
      toast(error.message);
    }
  });
  return form;
}
