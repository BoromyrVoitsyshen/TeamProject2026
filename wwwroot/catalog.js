// Головна сторінка: каталог майстрів із фільтрами.
// Фільтри живуть в адресі сторінки (?service=1&city=Київ…), тож посиланням можна поділитися,
// а «Назад» у браузері повертає попередній пошук.
'use strict';

const form = $('#filters');
const list = $('#masterList');
const title = $('#resultsTitle');
const filtersBox = $('#filtersBox');
const fields = ['service', 'city', 'q', 'minPrice', 'maxPrice', 'minExperience', 'minRating', 'sort'];
const wideScreen = matchMedia('(min-width: 900px)');

let services = [];
let requestNo = 0;

init();

async function init() {
  loadMe();
  syncFiltersPanel();
  wideScreen.addEventListener('change', syncFiltersPanel);

  try {
    const catalog = await api('/catalog');
    services = catalog.services;
    fillSelect($('#fService'), catalog.services.filter((s) => s.masters > 0)
      .map((s) => ({ value: s.id, label: `${s.name} (${s.masters})` })));
    fillSelect($('#fCity'), catalog.cities.map((c) => ({ value: c.name, label: `${c.name} (${c.masters})` })));
  } catch (error) {
    toast(error.message);
  }

  readFiltersFromUrl();
  form.addEventListener('input', (event) => {
    // текст і ціни — із затримкою, щоб не шукати на кожну літеру
    const typing = event.target.matches('input[type="search"], input[type="number"]');
    clearTimeout(form.timer);
    form.timer = setTimeout(search, typing ? 350 : 0);
  });
  // «Сортувати» стоїть поза формою (form="filters"), тож його подія до форми не доходить
  $('#fSort').addEventListener('change', () => search());
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    search();
  });
  form.addEventListener('reset', () => setTimeout(search)); // після того як браузер очистить поля
  addEventListener('popstate', () => {
    readFiltersFromUrl();
    search({ push: false });
  });

  search({ push: false });
}

function fillSelect(select, options) {
  const keep = select.value;
  select.append(...options.map((o) => h('option', { value: o.value }, o.label)));
  select.value = keep;
}

// На телефоні фільтри згорнуті, на широкому екрані завжди відкриті
function syncFiltersPanel() {
  filtersBox.open = wideScreen.matches;
}

function readFiltersFromUrl() {
  const params = new URLSearchParams(location.search);
  for (const name of fields) {
    const input = form.elements[name] ?? $(`[name="${name}"]`);
    if (!input) continue;
    const value = params.get(name) ?? (name === 'sort' ? 'rating' : '');
    input.value = value;
    // місто з посилання, якого вже немає у списку, — не губимо мовчки
    if (input.tagName === 'SELECT' && input.value !== value) input.value = '';
  }
}

function currentFilters() {
  const params = new URLSearchParams();
  for (const name of fields) {
    const input = form.elements[name] ?? $(`[name="${name}"]`);
    const value = input?.value.trim();
    if (value && !(name === 'sort' && value === 'rating')) params.set(name, value);
  }
  return params;
}

async function search({ push = true } = {}) {
  const params = currentFilters();
  const query = params.toString();
  if (push && query !== location.search.slice(1)) {
    history.pushState(null, '', query ? `?${query}` : location.pathname);
  }

  updatePriceLegend(params);
  const active = [...params.keys()].filter((k) => k !== 'sort').length;
  $('#filtersCount').hidden = active === 0;
  $('#filtersCount').textContent = active;

  const no = ++requestNo;
  list.setAttribute('aria-busy', 'true');
  try {
    const data = await api(`/masters${query ? `?${query}` : ''}`);
    if (no !== requestNo) return; // уже пішов новіший пошук
    render(data, params);
  } catch (error) {
    if (no !== requestNo) return;
    title.textContent = 'Не вдалося завантажити майстрів';
    list.replaceChildren(h('p', { className: 'empty-state' }, error.message));
  } finally {
    if (no === requestNo) list.removeAttribute('aria-busy');
  }
}

function updatePriceLegend(params) {
  const service = services.find((s) => String(s.id) === params.get('service'));
  $('#priceLegend').textContent = service ? `Ціна за «${service.name}», грн` : 'Ціна, грн';
}

function render(data, params) {
  title.textContent = data.total
    ? `${count(data.total, 'майстер', 'майстри', 'майстрів')}`
    : 'Нікого не знайдено';

  if (!data.total) {
    const reset = h('button', { className: 'btn btn-quiet', type: 'button' }, 'Скинути фільтри');
    reset.addEventListener('click', () => form.reset());
    list.replaceChildren(h('div', { className: 'empty-state' },
      h('p', {}, 'За цими фільтрами майстрів немає. Спробуйте інше місто або ширший діапазон цін.'),
      reset));
    return;
  }

  const serviceId = Number(params.get('service')) || null;
  list.replaceChildren(...data.masters.map((m) => card(m, serviceId)));
}

function card(master, serviceId) {
  const url = `master.html?id=${master.id}${serviceId ? `&service=${serviceId}` : ''}`;

  // Обрана послуга — першою, далі ще дві
  const offers = [...master.services].sort((a, b) => (b.serviceId === serviceId) - (a.serviceId === serviceId));
  const shown = offers.slice(0, 3);
  const more = offers.length - shown.length;

  const rating = master.reviewCount
    ? h('span', { className: 'rating' },
      h('span', { className: 'stars', ariaHidden: 'true' }, '★'),
      h('strong', {}, master.rating.toLocaleString('uk')),
      h('span', { className: 'muted' }, ` · ${count(master.reviewCount, 'відгук', 'відгуки', 'відгуків')}`))
    : h('span', { className: 'rating muted' }, 'Ще без відгуків');

  return h('article', { className: 'master-card' },
    h('div', { className: 'avatar', ariaHidden: 'true' }, initials(master.name)),
    h('div', { className: 'mc-body' },
      h('h3', { className: 'mc-name' }, h('a', { href: url }, master.name)),
      h('p', { className: 'mc-meta' },
        rating,
        h('span', { className: 'dot', ariaHidden: 'true' }, '·'),
        h('span', {}, `Стаж ${count(master.experienceYears, 'рік', 'роки', 'років')}`)),
      h('p', { className: 'mc-line' }, icon('pin'), h('span', {}, `${master.city}, ${master.address}`)),
      h('p', { className: 'mc-line' }, icon('clock'), h('span', {}, scheduleLine(master.schedule))),
      h('ul', { className: 'chips', ariaLabel: 'Послуги' },
        shown.map((s) => h('li', { className: s.serviceId === serviceId ? 'chip chip-on' : 'chip' },
          `${s.name} · ${money(s.price)}`)),
        more > 0 ? h('li', { className: 'chip chip-more' }, `ще ${more}`) : null)),
    h('div', { className: 'mc-side' },
      h('p', { className: 'price' },
        master.priceIsFrom ? h('span', { className: 'muted' }, 'від ') : null,
        money(master.price)),
      h('a', { className: 'btn btn-primary', href: url, ariaLabel: `Дізнатися більше про ${master.name}` }, 'Дізнатися більше')));
}
