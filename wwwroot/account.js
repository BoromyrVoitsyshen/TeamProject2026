// Кабінет. Клієнт бачить свої записи; майстер — профіль і розклад, згенерований графік і записи клієнтів.
'use strict';

const page = $('#page');
const weekdayNames = ['Понеділок', 'Вівторок', 'Середа', 'Четвер', 'Пʼятниця', 'Субота', 'Неділя'];

init();

async function init() {
  const user = await loadMe();
  if (!user) {
    location.replace('login.html?next=account.html');
    return;
  }
  if (user.role === 'master') await renderMaster(user);
  else await renderClient(user);
}

// ═════════════ Клієнт ═════════════
async function renderClient(user) {
  document.title = 'Мої записи — Mechs';
  const list = h('div', { className: 'stack-lg' });
  $('#loading').replaceWith(
    h('div', { className: 'account-head' },
      h('div', {}, h('h1', {}, 'Мої записи'), h('p', { className: 'muted' }, `${user.name} · ${user.email}`)),
      h('a', { className: 'btn btn-primary', href: './' }, 'Знайти майстра')),
    list);
  await fillBookings(list, 'client');
}

// ═════════════ Записи (спільне для клієнта й майстра) ═════════════
async function fillBookings(container, role) {
  container.replaceChildren(h('p', { className: 'loading' }, 'Завантаження…'));
  let data;
  try {
    data = await api('/bookings/my');
  } catch (error) {
    container.replaceChildren(h('p', { className: 'empty-state' }, error.message));
    return;
  }

  const upcoming = data.bookings.filter((b) => b.state === 'upcoming').sort((a, b) => a.startsAt.localeCompare(b.startsAt));
  const past = data.bookings.filter((b) => b.state !== 'upcoming');

  if (!data.bookings.length) {
    container.replaceChildren(h('div', { className: 'empty-state' },
      role === 'client'
        ? [h('p', {}, 'Ще немає записів.'), h('a', { className: 'btn btn-primary', href: './' }, 'Знайти майстра')]
        : [h('p', {}, 'До вас ще ніхто не записався. Перевірте, що профіль опубліковано і в розкладі є вільний час.')]));
    return;
  }

  const section = (title, items, empty) => h('section', { className: 'stack' },
    h('h2', {}, title),
    items.length
      ? h('ul', { className: 'booking-list' }, items.map((b) => bookingItem(b, role, () => fillBookings(container, role))))
      : h('p', { className: 'muted' }, empty));

  container.replaceChildren(
    section(`Найближчі · ${upcoming.length}`, upcoming, 'Найближчих записів немає.'),
    section('Минулі та скасовані', past, 'Поки нічого.'));
}

function bookingItem(b, role, refresh) {
  const date = parseDate(b.startsAt);
  const states = {
    upcoming: ['chip chip-on', 'Заплановано'],
    done: ['chip', 'Відбувся'],
    cancelled: ['chip chip-off', b.cancelledBy === 'master' ? 'Скасував майстер' : 'Скасував клієнт'],
  };
  const [chipClass, chipText] = states[b.state];

  const who = role === 'client'
    ? h('p', {}, h('a', { href: `master.html?id=${b.master.id}` }, b.master.name), h('span', { className: 'muted' }, ` · ${b.master.city}, ${b.master.address}`))
    : h('p', {}, h('strong', {}, b.client?.name ?? 'Клієнт'),
      b.client?.phone ? [' · ', h('a', { href: `tel:${b.client.phone.replace(/[^\d+]/g, '')}` }, b.client.phone)] : null,
      b.client?.email ? h('span', { className: 'muted' }, ` · ${b.client.email}`) : null);

  const actions = [];
  if (b.canCancel) {
    const cancel = h('button', { className: 'btn btn-quiet btn-sm', type: 'button' }, 'Скасувати');
    cancel.addEventListener('click', async () => {
      if (!confirm(`Скасувати запис на ${dayMonth(b.startsAt)}, ${timeOf(b.startsAt)}?`)) return;
      cancel.disabled = true;
      try {
        await api(`/bookings/${b.id}/cancel`, { method: 'POST' });
        toast('Запис скасовано');
        refresh();
      } catch (error) {
        cancel.disabled = false;
        toast(error.message);
      }
    });
    actions.push(cancel);
  }
  if (role === 'client' && b.canReview) {
    actions.push(h('a', { className: 'btn btn-quiet btn-sm', href: `master.html?id=${b.master.id}#review` }, 'Залишити відгук'));
  }

  return h('li', { className: `booking-item${b.state === 'cancelled' ? ' is-cancelled' : ''}` },
    h('div', { className: 'booking-date', ariaHidden: 'true' },
      h('span', { className: 'date-d' }, date.getDate()),
      h('span', { className: 'date-m' }, formats.dayMonthShort.format(date).replace(/^\d+\s*/, ''))),
    h('div', { className: 'booking-body' },
      h('p', { className: 'booking-title' },
        h('strong', {}, `${longDate(b.startsAt)}, ${timeOf(b.startsAt)}–${timeOf(b.endsAt)}`),
        h('span', { className: chipClass }, chipText)),
      h('p', {}, `${b.service} · ${money(b.price)}`),
      who,
      b.comment ? h('p', { className: 'booking-comment' }, `«${b.comment}»`) : null),
    actions.length ? h('div', { className: 'booking-actions' }, actions) : null);
}

// ═════════════ Майстер ═════════════
let profile = null;
let catalogServices = [];

async function renderMaster(user) {
  document.title = 'Кабінет майстра — Mechs';
  try {
    [profile, { services: catalogServices }] = await Promise.all([api('/my/master'), api('/catalog')]);
  } catch (error) {
    $('#loading').replaceWith(h('p', { className: 'empty-state' }, error.message));
    return;
  }

  const status = h('span', { id: 'publishState' });
  const panels = {
    profile: h('section', { id: 'profile', role: 'tabpanel', ariaLabelledby: 'tab-profile' }),
    timetable: h('section', { id: 'timetable', role: 'tabpanel', ariaLabelledby: 'tab-timetable' }),
    bookings: h('section', { id: 'bookings', role: 'tabpanel', ariaLabelledby: 'tab-bookings' }),
  };
  const tabNames = { profile: 'Профіль і розклад', timetable: 'Графік', bookings: 'Записи' };
  const tabs = Object.entries(tabNames).map(([key, label]) =>
    h('a', { className: 'tab', id: `tab-${key}`, href: `#${key}`, role: 'tab' }, label));

  $('#loading').replaceWith(
    h('div', { className: 'account-head' },
      h('div', {},
        h('h1', {}, 'Кабінет майстра'),
        h('p', { className: 'muted' }, status, ' · ', h('a', { href: `master.html?id=${profile.id}` }, 'Як мене бачать клієнти'))),
    ),
    h('nav', { className: 'tabs', role: 'tablist', ariaLabel: 'Розділи кабінету' }, tabs),
    ...Object.values(panels));

  renderPublishState();
  panels.profile.append(profileForm());

  const route = () => {
    const key = location.hash.slice(1) in panels ? location.hash.slice(1) : 'profile';
    for (const [name, panel] of Object.entries(panels)) panel.hidden = name !== key;
    for (const tab of tabs) tab.setAttribute('aria-selected', String(tab.id === `tab-${key}`));
    // Графік і записи завантажуємо щоразу заново: після збереження розкладу чи нового запису вони змінюються
    if (key === 'timetable') renderTimetable(panels.timetable, 0);
    if (key === 'bookings') fillBookings(panels.bookings, 'master');
  };
  addEventListener('hashchange', route);
  route();
}

function renderPublishState() {
  const el = $('#publishState');
  el.className = profile.isPublished ? 'chip chip-on' : 'chip';
  el.textContent = profile.isPublished ? 'Опубліковано в каталозі' : 'Чернетка — клієнти не бачать';
}

// ───────────── Профіль і розклад ─────────────
function profileForm() {
  const input = (id, props) => h('input', { id, ...props });
  const field = (id, label, control, hint) => h('div', { className: 'field' },
    h('label', { for: id }, label), control, hint ? h('span', { className: 'muted small' }, hint) : null);

  // Послуги: увесь перелік, позначені — ті, що майстер робить
  const offered = new Map(profile.services.map((s) => [s.serviceId, s]));
  const serviceRows = catalogServices.map((service) => {
    const mine = offered.get(service.id);
    const check = h('input', { type: 'checkbox', id: `svc-${service.id}`, checked: Boolean(mine), dataset: { service: service.id } });
    const price = h('input', { type: 'number', min: 1, max: 1000000, step: 10, inputMode: 'numeric', value: mine?.price ?? '', disabled: !mine, ariaLabel: `Ціна: ${service.name}, грн`, className: 'svc-price' });
    const minutes = h('select', { disabled: !mine, ariaLabel: `Тривалість: ${service.name}`, className: 'svc-duration' },
      [15, 20, 30, 45, 60, 90, 120, 150, 180, 240, 300, 360, 480].map((m) => h('option', { value: m }, duration(m))));
    minutes.value = mine?.durationMinutes ?? 60;
    if (mine && minutes.value !== String(mine.durationMinutes)) {
      minutes.prepend(h('option', { value: mine.durationMinutes }, duration(mine.durationMinutes)));
      minutes.value = mine.durationMinutes;
    }
    check.addEventListener('change', () => {
      price.disabled = minutes.disabled = !check.checked;
      if (check.checked) price.focus();
    });
    return h('li', { className: 'svc-row' },
      h('label', { className: 'check', for: `svc-${service.id}` }, check, h('span', {}, service.name)),
      h('span', { className: 'svc-inputs' }, price, h('span', { className: 'muted' }, 'грн'), minutes));
  });

  // Розклад: 7 днів, у кожного — «працюю» і години
  const days = new Map(profile.days.map((d) => [d.weekday, d]));
  const dayRows = weekdayNames.map((name, i) => {
    const weekday = i + 1;
    const day = days.get(weekday);
    const on = h('input', { type: 'checkbox', id: `day-${weekday}`, checked: Boolean(day), dataset: { weekday } });
    const start = h('input', { type: 'time', step: 900, value: day?.start ?? '09:00', disabled: !day, ariaLabel: `${name}: початок`, className: 'day-start' });
    const end = h('input', { type: 'time', step: 900, value: day?.end ?? '18:00', disabled: !day, ariaLabel: `${name}: кінець`, className: 'day-end' });
    on.addEventListener('change', () => { start.disabled = end.disabled = !on.checked; });
    return h('li', { className: 'day-row' },
      h('label', { className: 'check', for: `day-${weekday}` }, on, h('span', {}, name)),
      h('span', { className: 'day-hours' }, start, h('span', { ariaHidden: 'true' }, '–'), end));
  });

  const copyHours = h('button', { className: 'btn btn-quiet btn-sm', type: 'button' }, 'Години першого робочого дня — на всі');
  copyHours.addEventListener('click', () => {
    const rows = dayRows.filter((row) => row.querySelector('input[type="checkbox"]').checked);
    if (!rows.length) return toast('Позначте хоча б один робочий день.');
    const [start, end] = [rows[0].querySelector('.day-start').value, rows[0].querySelector('.day-end').value];
    for (const row of rows) {
      row.querySelector('.day-start').value = start;
      row.querySelector('.day-end').value = end;
    }
  });

  const step = h('select', { id: 'pStep' }, [15, 20, 30, 45, 60].map((m) => h('option', { value: m }, `кожні ${m} хв`)));
  step.value = profile.slotStepMinutes;
  const horizon = h('select', { id: 'pHorizon' }, [7, 14, 21, 30, 45, 60].map((d) => h('option', { value: d }, count(d, 'день', 'дні', 'днів'))));
  horizon.value = profile.bookingHorizonDays;
  if (horizon.value !== String(profile.bookingHorizonDays)) {
    horizon.prepend(h('option', { value: profile.bookingHorizonDays }, count(profile.bookingHorizonDays, 'день', 'дні', 'днів')));
    horizon.value = profile.bookingHorizonDays;
  }

  // Вихідні дати
  let daysOff = [...profile.daysOff];
  const offList = h('ul', { className: 'chips' });
  const renderOff = () => offList.replaceChildren(...(daysOff.length
    ? daysOff.map((date) => {
      const remove = h('button', { type: 'button', className: 'chip-x', ariaLabel: `Прибрати ${dayMonth(date)}` }, '×');
      remove.addEventListener('click', () => { daysOff = daysOff.filter((d) => d !== date); renderOff(); });
      return h('li', { className: 'chip' }, longDate(date), remove);
    })
    : [h('li', { className: 'muted small' }, 'Немає — працюєте за розкладом.')]));
  renderOff();
  const today = new Date();
  const todayIso = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
  const offInput = h('input', { type: 'date', id: 'pDayOff', min: todayIso });
  const addOff = h('button', { className: 'btn btn-quiet btn-sm', type: 'button' }, 'Додати');
  addOff.addEventListener('click', () => {
    if (!offInput.value) return toast('Оберіть дату.');
    if (!daysOff.includes(offInput.value)) daysOff = [...daysOff, offInput.value].sort();
    offInput.value = '';
    renderOff();
  });

  const published = h('input', { type: 'checkbox', id: 'pPublished', checked: profile.isPublished });
  const error = h('p', { className: 'form-error', role: 'alert', hidden: true });
  const save = h('button', { className: 'btn btn-primary', type: 'submit' }, 'Зберегти');

  const form = h('form', { className: 'cabinet-form', noValidate: true },
    h('section', { className: 'panel' },
      h('h2', {}, 'Про вас'),
      field('pName', 'Ім’я або назва майстерні', input('pName', { value: profile.displayName, maxLength: 80, required: true })),
      field('pDescription', 'Опис', h('textarea', { id: 'pDescription', rows: 4, maxLength: 2000, value: profile.description,
        placeholder: 'З якими авто працюєте, що вмієте найкраще' })),
      h('div', { className: 'grid-2' },
        field('pExperience', 'Стаж, років', input('pExperience', { type: 'number', min: 0, max: 60, value: profile.experienceYears, inputMode: 'numeric' })),
        field('pPhone', 'Телефон для клієнтів', input('pPhone', { type: 'tel', value: profile.phone ?? '', maxLength: 30 })))),
    h('section', { className: 'panel' },
      h('h2', {}, 'Де ви працюєте'),
      h('div', { className: 'grid-2' },
        field('pCity', 'Місто', input('pCity', { value: profile.city, maxLength: 60, placeholder: 'Київ' })),
        field('pAddress', 'Адреса', input('pAddress', { value: profile.address, maxLength: 200, placeholder: 'вул. Хрещатик, 1' })))),
    h('section', { className: 'panel' },
      h('h2', {}, 'Послуги, ціни й тривалість'),
      h('p', { className: 'muted small' }, 'Тривалість визначає, скільки часу займе запис у графіку.'),
      h('ul', { className: 'svc-list' }, serviceRows)),
    h('section', { className: 'panel' },
      h('h2', {}, 'Розклад'),
      h('p', { className: 'muted small' }, 'Позначте робочі дні й години — сайт сам згенерує вільний час для запису. Записи, які вже є, зміна розкладу не скасує.'),
      h('ul', { className: 'day-list' }, dayRows),
      copyHours,
      h('div', { className: 'grid-2' },
        field('pStep', 'Час запису', step, 'З яким кроком показувати час: 09:00, 09:30…'),
        field('pHorizon', 'Записи наперед', horizon, 'На скільки днів уперед можна записатися')),
      h('div', { className: 'field' },
        h('label', { for: 'pDayOff' }, 'Вихідні дати (відпустка, свята)'),
        h('div', { className: 'inline' }, offInput, addOff),
        offList)),
    h('section', { className: 'panel' },
      h('h2', {}, 'Публікація'),
      h('label', { className: 'check', for: 'pPublished' }, published,
        h('span', {}, 'Показувати мене в каталозі майстрів')),
      h('p', { className: 'muted small' }, 'Для публікації потрібні місто, адреса, хоча б одна послуга й один робочий день.')),
    h('div', { className: 'save-bar' }, error, save));

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    error.hidden = true;

    const services = serviceRows
      .map((row) => row.querySelector('input[type="checkbox"]'))
      .filter((check) => check.checked)
      .map((check) => {
        const row = check.closest('li');
        return {
          serviceId: Number(check.dataset.service),
          price: Number(row.querySelector('.svc-price').value),
          durationMinutes: Number(row.querySelector('.svc-duration').value),
        };
      });
    const missingPrice = services.find((s) => !(s.price > 0));
    if (missingPrice) {
      const name = catalogServices.find((s) => s.id === missingPrice.serviceId)?.name;
      error.textContent = `Вкажіть ціну для «${name}».`;
      error.hidden = false;
      return;
    }

    const body = {
      displayName: $('#pName').value,
      description: $('#pDescription').value,
      city: $('#pCity').value,
      address: $('#pAddress').value,
      phone: $('#pPhone').value,
      experienceYears: Number($('#pExperience').value) || 0,
      slotStepMinutes: Number(step.value),
      bookingHorizonDays: Number(horizon.value),
      isPublished: published.checked,
      services,
      days: dayRows
        .filter((row) => row.querySelector('input[type="checkbox"]').checked)
        .map((row) => ({
          weekday: Number(row.querySelector('input[type="checkbox"]').dataset.weekday),
          start: row.querySelector('.day-start').value,
          end: row.querySelector('.day-end').value,
        })),
      daysOff,
    };

    save.disabled = true;
    try {
      profile = await api('/my/master', { method: 'PUT', body });
      daysOff = [...profile.daysOff];
      renderOff();
      renderPublishState();
      toast(profile.isPublished ? 'Збережено. Профіль видно в каталозі.' : 'Збережено.');
    } catch (err) {
      error.textContent = err.message;
      error.hidden = false;
      error.scrollIntoView({ block: 'nearest' });
    } finally {
      save.disabled = false;
    }
  });

  return form;
}

// ───────────── Згенерований графік ─────────────
async function renderTimetable(panel, weekOffset) {
  const start = new Date();
  start.setDate(start.getDate() + weekOffset * 7);
  const from = `${start.getFullYear()}-${String(start.getMonth() + 1).padStart(2, '0')}-${String(start.getDate()).padStart(2, '0')}`;

  panel.replaceChildren(h('p', { className: 'loading' }, 'Завантаження…'));
  let data;
  try {
    data = await api(`/my/master/timetable?from=${from}&days=7`);
  } catch (error) {
    panel.replaceChildren(h('p', { className: 'empty-state' }, error.message));
    return;
  }

  const prev = h('button', { className: 'btn btn-quiet btn-sm', type: 'button', disabled: weekOffset <= 0 }, '← Попередній тиждень');
  const next = h('button', { className: 'btn btn-quiet btn-sm', type: 'button' }, 'Наступний тиждень →');
  prev.addEventListener('click', () => renderTimetable(panel, weekOffset - 1));
  next.addEventListener('click', () => renderTimetable(panel, weekOffset + 1));

  const first = data.days[0].date;
  const last = data.days.at(-1).date;

  panel.replaceChildren(
    h('div', { className: 'tt-bar' },
      h('h2', {}, `${dayMonth(first)} — ${dayMonth(last)}`),
      h('div', { className: 'inline' }, prev, next)),
    h('p', { className: 'muted small' },
      `Графік згенеровано з вашого розкладу, крок — ${data.slotStepMinutes} хв. `,
      h('span', { className: 'legend free' }, 'вільно'), ' ',
      h('span', { className: 'legend booked' }, 'зайнято'), ' ',
      h('span', { className: 'legend past' }, 'минуло')),
    h('ol', { className: 'timetable' }, data.days.map((day) => h('li', { className: day.isWorking ? 'tt-day' : 'tt-day off' },
      h('div', { className: 'tt-head' },
        h('strong', {}, `${day.weekday}, ${dayMonth(day.date)}`),
        h('span', { className: 'muted small' }, day.isWorking
          ? `${day.start}–${day.end} · вільно ${day.freeCount} з ${day.cells.length}`
          : day.isDayOff ? 'Вихідна дата' : 'Вихідний за розкладом')),
      day.isWorking
        ? h('div', { className: 'tt-cells', role: 'img', ariaLabel: `${day.weekday}: ${day.freeCount} вільних із ${day.cells.length}` },
          day.cells.map((c) => h('span', { className: `tt-cell ${c.state}`, title: `${c.time} — ${{ free: 'вільно', booked: 'зайнято', past: 'минуло' }[c.state]}` }, c.time)))
        : null,
      day.bookings.length
        ? h('ul', { className: 'tt-bookings' }, day.bookings.map((b) =>
          h('li', {}, h('strong', {}, `${b.start}–${b.end}`), ` ${b.service} · ${b.client}`)))
        : null))));
}
