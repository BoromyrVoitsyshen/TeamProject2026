# AI Chat - .NET Backend

This repository contains the backend for our team's AI chat project. The original Node.js concept was rewritten in **C# (ASP.NET Core)** for better scalability, typing and easier extension in the future.

The project is built on the principles of **Vertical Slice Architecture** (each feature has its own isolated folder with its logic) using **Minimal APIs**.

## How to run (for the whole team)

1. Make sure you have the [.NET 8 SDK](https://dotnet.microsoft.com/download) installed (or a newer version).
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

## For the AI engineer (AI integration)
Right now a stub (StubChatAgent) replies instead of a real model.
All AI code lives in the Agent/ folder — endpoints don't need to be changed.
### AI module (Agent/)
| File | Purpose |
|---|---|
| IChatAgent.cs | The only entry point to the model: ReplyAsync(history, ct) → reply text |
| StubChatAgent.cs | Stub for development without an API key |
| AgentReplyRunner.cs | Shared logic for POST /messages and POST /reply: timeout, 502 on error, saving the reply |
| AgentOptions.cs | The Agent config section (timeout, key, model) |
| Prompts/system-prompt.md | The bot's system prompt (car service) |
### How to connect a model
1. Create a class that implements IChatAgent.
2. In Program.cs, replace StubChatAgent with it:
   builder.Services.AddSingleton<IChatAgent, YourAgent>();
### Already implemented — no need to redo
- Timeout Agent:TimeoutSeconds (120 s), not tied to closing the tab.
- An exception or empty reply → 502, the page shows "Try again" (POST /api/chats/{id}/reply).
- One agent reply per conversation at a time (409), the page polls busy.
- History: the whole conversation is passed to ReplyAsync; the last element is the new user message.
- Photos: URL /uploads/x.png → path on disk via ChatStorage.TryGetUploadPath.
### Configuration
| Key | Default | Where to set it |
|---|---|---|
| Agent:TimeoutSeconds | 120 | appsettings.json |
| Agent:Endpoint | — | appsettings.json |
| Agent:Model | — | appsettings.json |
| Agent:ApiKey | — | user-secrets / env only, never in git |
# Locally
dotnet user-secrets set "Agent:ApiKey" "<key>"
# Docker
docker run -e Agent__ApiKey=<key> ...
Provider and model choice: see docs/ai/model-selection.md (MECH-51).
### Where to write code:
Your main entry point is the file:
`Endpoints/SendMessage/Endpoint.cs` (or the corresponding feature folder).

### What to keep in mind:
1. **Current implementation:** Right now the method accepts a message, saves it and instantly generates a fake reply (`role: "assistant"`).
2. **Asynchrony and timeouts:** Since generating an AI reply can take a long time (up to 1–2 minutes), keeping the HTTP request open is not always safe. Consider moving the model call into a background process (`IHostedService` or `BackgroundWorker`) while the backend immediately returns a status code to the front end (the front end can poll the server for status).
3. **Images:** Images arrive as `multipart/form-data`, and the backend already saves them to disk. You will need to convert these saved files to `Base64` (or another format required by your model's API) before sending the prompt.
4. **Conversation context:** The history of the whole chat is already available in the `chat.Messages` object. It is enough to pass it to the model to preserve context.

---

## Project structure (for the backend)

* `wwwroot/` — front-end static files.
* `Domain/` — basic data models (`ChatSession`, `ChatMessage`).
* `Infrastructure/` — file system work (`ChatStorage.cs`).
* `Endpoints/` — all API routes (slices) live here. Each folder is responsible for one specific action (for example, `CreateChat`, `GetChats`, `SendMessage`).
* `data/` — generated automatically. It holds the `.json` conversation files and the uploaded photos (`uploads/`). **This folder is added to .gitignore.**
* `Agent/` — AI module: the `IChatAgent interface`, the stub, `AgentReplyRunner`, configuration and prompts.
## Data and DB
While the project is at the prototype stage, all conversations are stored in plain JSON files in the `data/chats/` folder. No database needs to be set up. When the project scales, we will replace `ChatStorage` with Entity Framework Core without changes to the controller logic.

## Design link
https://www.figma.com/design/g8swP7duhrvI3IWAdLCoYh/CiteDesign?m=auto&t=N4wkvVdnlqKdQ9WJ-1

## Blueprint link
https://docs.google.com/document/d/1o1KD3aPaxhAhXGFQFTv9lYqB_miqYe9v/edit?usp=drive_link&ouid=104580997232242831904&rtpof=true&sd=true
