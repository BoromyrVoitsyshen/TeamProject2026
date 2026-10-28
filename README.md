# AI Chat - .NET Backend

This repository contains the backend for our team's AI chat project. The original Node.js concept was rewritten in **C# (ASP.NET Core)** for better scalability, typing and easier extension in the future.

The project is built on the principles of **Vertical Slice Architecture** (each feature has its own isolated folder with its logic) using **Minimal APIs**.

## How to run (for the whole team)

1. Make sure you have the [.NET 10 SDK](https://dotnet.microsoft.com/download) installed (the project targets `net10.0`).
2. Open the project in Visual Studio / Rider or use the terminal:

    dotnet run

3. The server will start and the console will show the address (usually `https://localhost:7106` or `http://localhost:5079`).
4. Open this address in your browser — the front end will load automatically.

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

## Project structure (for the backend)

* `wwwroot/` — front-end static files.
* `Domain/` — basic data models (`ChatSession`, `ChatMessage`).
* `Infrastructure/` — file system work (`ChatStorage.cs`), image checks, busy tracking.
* `Agent/` — the AI model behind `IChatAgent` (OpenRouter or the stub).
* `Endpoints/` — all API routes (slices) live here. Each folder is responsible for one specific action (for example, `CreateChat`, `GetChats`, `SendMessage`).
* `data/` — generated automatically. It holds the `.json` conversation files and the uploaded photos (`uploads/`). **This folder is added to .gitignore.**

## Data and DB
While the project is at the prototype stage, all conversations are stored in plain JSON files in the `data/chats/` folder. No database needs to be set up. When the project scales, we will replace `ChatStorage` with Entity Framework Core without changes to the controller logic.

## Design link
https://www.figma.com/design/g8swP7duhrvI3IWAdLCoYh/CiteDesign?m=auto&t=N4wkvVdnlqKdQ9WJ-1

## Blueprint link
https://docs.google.com/document/d/1o1KD3aPaxhAhXGFQFTv9lYqB_miqYe9v/edit?usp=drive_link&ouid=104580997232242831904&rtpof=true&sd=true
