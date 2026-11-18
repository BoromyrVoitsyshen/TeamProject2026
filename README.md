# Mechs — masters catalogue and AI chat (.NET)

This repository contains the site for our team project: a catalogue of car masters with online booking and reviews, plus an AI assistant chat. The original Node.js concept was rewritten in **C# (ASP.NET Core)** for better scalability, typing and easier extension in the future.

The project is built on the principles of **Vertical Slice Architecture** (each feature has its own isolated folder with its logic) using **Minimal APIs**.

## How to run (for the whole team)

1. Make sure you have the [.NET 10 SDK](https://dotnet.microsoft.com/download) installed (the project targets `net10.0`).
2. Open the project in Visual Studio / Rider or use the terminal:

    dotnet run

3. The server will start and the console will show the address (usually `https://localhost:7106` or `http://localhost:5079`).
4. Open this address in your browser — the masters catalogue opens; the AI chat is at `/chat.html`.

On the first run the server creates the database `data/app.db` and fills it with demo masters, clients, bookings and reviews. Nothing needs to be installed: SQLite is a single file.

---

## For the Front-end developer

The backend fully supports the agreed API contracts. You don't need to change anything in your requests.

* **Where your files are:** The whole client part (HTML, CSS, JS) now lives in the `wwwroot/` folder. The server serves it automatically on startup.
* **API:** All routes (`GET /api/chats`, `POST /api/chats/:id/messages`, etc.) work as expected.
* **Fix:** The `updatedAt` field, which was missing in the original models, has been added to the conversation objects, so sorting and time display in the sidebar now work correctly.
* **Files:** Uploaded photos are stored locally and served at `/uploads/...`.

---

## AI (OpenRouter)

Replies come from models on [OpenRouter](https://openrouter.ai/docs) — one API for many providers. Without a key the site still works with a stub (`StubChatAgent`), so you can run it without your own key.

### Connect your key (once per machine)

1. Create a key at https://openrouter.ai/settings/keys.
2. In the project folder run:

    dotnet user-secrets set "Agent:ApiKey" "sk-or-v1-..."

3. `dotnet run` — the console prints `Chat agent: OpenRouter, models: ...` and, a moment later, either `Agent:Models checked: ...` or a warning about a model.

If the settings are wrong (for example, more than 3 models), the app does not start and prints `Hosting failed to start` with the reason.

**Never** put the key in `appsettings.json`, `wwwroot/` or a commit. In Docker pass it as the environment variable `Agent__ApiKey`.

### Settings (`appsettings.json`, section `Agent`)

| Setting | Default | Meaning |
|---|---|---|
| `Models` | `nvidia/nemotron-3.5-super-vl:free`, then `openrouter/free` | Chat models in priority order; OpenRouter falls back to the next one. OpenRouter accepts **at most 3** (otherwise HTTP 400 `'models' array must have 3 items or fewer`), use only chat models (not `embed`/`rerank`) and only `:free` ones, or a fallback may be billed. |
| `MaxHistoryMessages` | 20 | How many recent messages are sent to the model |
| `MaxTokens` | 1500 | Maximum reply length, including the model's hidden reasoning tokens |
| `TimeoutSeconds` | 120 | After this the page shows "Try again" (HTTP 502) |
| `CheckModelsOnStartup` | true | On startup, check `Models` against the OpenRouter model list (exists, replies with text, free) and print warnings. Set to `false` to work offline. |

Free models allow 20 requests per minute and 50 per day for accounts that never bought credits (1000 per day after buying at least 10 credits). Free providers may log prompts, so do not send personal data.

### Code

* `Agent/IChatAgent.cs` — the only thing endpoints know about the model.
* `Agent/OpenRouter/OpenRouterChatAgent.cs` — the HTTP call, history trimming, error handling.
* `Agent/SystemPrompt.cs` — the assistant's role: cars only, short answers in Ukrainian.
* `Agent/AgentReplyRunner.cs` — timeout, 502, saving the reply; shared by `/messages` and `/reply`.

Photos are not sent to the model yet; it only sees a `[фото]` marker.

---

## Masters catalogue, booking and reviews

### Pages (`wwwroot/`)

| Page | What it does |
|---|---|
| `index.html` + `catalog.js` | Main page: list of masters with filters by service, city, address or name, price, experience and rating; sorting. Filters live in the URL, so a search can be shared. |
| `master.html` + `master.js` | "Learn more": description, phone, services with prices and durations, weekly schedule, booking (service → day → free time), reviews with a rating breakdown and a form. |
| `login.html` + `login.js` | Sign in and registration as a **client** or a **master**. |
| `account.html` + `account.js` | Client: own bookings, cancel, leave a review. Master: profile, services and prices, weekly schedule, days off, publishing; the generated timetable; bookings with client contacts. |
| `app.js`, `site.css` | Shared helpers (API calls, header with sign-in) and styles; colours and fonts come from `styles.css`, the same design as the chat. |

### Demo accounts

All demo accounts use the password `demo12345`. The names and addresses are made up.

| Email | Who |
|---|---|
| `master1@example.com` … `master6@example.com` | Published masters in Kyiv, Lviv, Kharkiv, Odesa, Dnipro |
| `master7@example.com` | Master with an unpublished profile (try publishing it) |
| `client1@example.com` | Client with an upcoming booking and a past visit that can still be reviewed |
| `client2@example.com`, `client3@example.com` | Clients with past visits and reviews |

Turn the demo data off with `"Database": { "SeedDemoData": false }`. It is only added to an empty database.

### How the schedule is generated

A master sets working days and hours (for example Wed, Thu, Fri 09:00–18:00), a slot step (15–60 min), how many days ahead clients can book (1–60) and single days off. Each service has its own price and duration.

Free times are **not stored**. `Scheduling/ScheduleGenerator.cs` computes them on every request: from the start of each working day, in steps of the slot step, a time is free if the whole service fits before the end of the day, does not overlap an existing booking and starts at least 30 minutes from now. So a schedule change takes effect immediately, and existing bookings stay as they are.

Booking runs under a per-master lock (`Scheduling/BookingLocks.cs`): the chosen time is checked against freshly generated slots and saved in one step, so two clients cannot take the same time. Times are local workshop time (`Booking:TimeZone`, default `Europe/Kyiv`).

### Rules

* Only a **client** can book; a booking can be cancelled by the client or the master until it starts.
* A review (1–5 stars, up to 1000 characters) can be left only by a client who has had a visit to that master, once per master.
* A master appears in the catalogue only when the profile is published and has a city, an address, at least one service and one working day.
* Passwords are stored as PBKDF2-SHA256 hashes; sign-in uses an HttpOnly cookie; login and registration are limited to 10 attempts per minute per IP.

### API

Errors always come as `{ "error": "text" }`.

| Request | Who | What |
|---|---|---|
| `POST /api/auth/register` | anyone | `{ name, email, password, phone?, role: "client" \| "master" }` → signs in |
| `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me` | anyone | Sign in / out, current user (`{ "user": null }` for guests) |
| `GET /api/catalog` | anyone | Services (with master counts), cities, price range — for the filters |
| `GET /api/masters?service=&city=&q=&minPrice=&maxPrice=&minExperience=&minRating=&sort=` | anyone | Catalogue; `sort` = `rating` (default), `reviews`, `price`, `experience` |
| `GET /api/masters/{id}` | anyone | Full profile, reviews, what the viewer can do |
| `GET /api/masters/{id}/slots?serviceId=&from=&days=` | anyone | Free times per day for a service |
| `POST /api/bookings` | client | `{ masterId, serviceId, date: "2026-11-12", time: "09:30", comment? }` |
| `GET /api/bookings/my` | client, master | Own bookings (client) or bookings to me (master) |
| `POST /api/bookings/{id}/cancel` | client, master | Cancel a booking that has not started |
| `POST /api/masters/{id}/reviews` | client | `{ rating, text }` |
| `GET /api/my/master`, `PUT /api/my/master` | master | Read / save profile, services, schedule, days off, publishing |
| `GET /api/my/master/timetable?from=&days=` | master | Generated timetable with booked cells |

### Database

The database is SQLite through Entity Framework Core (`Data/AppDbContext.cs`, tables in `Data/Entities.cs`). The file is `data/app.db` (folder from `Storage:DataPath`, already in `.gitignore`).

The database is created with `EnsureCreated`, which does **not** update an existing database. After changing the models, delete `data/app.db` (the demo data will be created again). When we start keeping real data, switch to migrations:

    dotnet tool install --global dotnet-ef
    dotnet ef migrations add Initial
    # and in Data/DatabaseSetup.cs replace EnsureCreatedAsync() with MigrateAsync()

In Docker, mount a volume at `/app/data`, otherwise accounts and bookings disappear with the container.

---

## Project structure (for the backend)

* `wwwroot/` — front-end static files.
* `Domain/` — chat data models (`ChatSession`, `ChatMessage`).
* `Data/` — the SQLite database: entities, `AppDbContext`, creation and demo data.
* `Accounts/` — password hashing, cookie sign-in, current user, rate limit.
* `Catalog/` — masters for the catalogue (`MasterCatalog`) and review rules.
* `Scheduling/` — local time, schedule generation, booking locks.
* `Infrastructure/` — file system work (`ChatStorage.cs`), image checks, busy tracking.
* `Agent/` — the AI model behind `IChatAgent` (OpenRouter or the stub).
* `Endpoints/` — all API routes (slices) live here. Each folder is responsible for one specific action (for example, `CreateChat`, `GetChats`, `SendMessage`); the catalogue slices are grouped in `Accounts/`, `Catalog/`, `Bookings/`, `Reviews/` and `MasterCabinet/`.
* `data/` — generated automatically. It holds the database `app.db`, the `.json` conversation files and the uploaded photos (`uploads/`). **This folder is added to .gitignore.**

## Data and DB
Accounts, masters, bookings and reviews live in the SQLite database `data/app.db` (see "Database" above). Conversations with the AI assistant are still stored in plain JSON files in `data/chats/`; they can move to the same database later by replacing `ChatStorage`.

## Design link
https://www.figma.com/design/g8swP7duhrvI3IWAdLCoYh/CiteDesign?m=auto&t=N4wkvVdnlqKdQ9WJ-1

## Blueprint link
https://docs.google.com/document/d/1o1KD3aPaxhAhXGFQFTv9lYqB_miqYe9v/edit?usp=drive_link&ouid=104580997232242831904&rtpof=true&sd=true
