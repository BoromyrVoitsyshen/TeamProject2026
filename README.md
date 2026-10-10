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

Right now, real artificial intelligence is **not connected**. A hardcoded stub replies instead. Your task is to replace it with a call to a real model (for example, via Semantic Kernel, the official SDKs from OpenAI / Anthropic / Google, or custom HTTP requests to local Python services).

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

## Data and DB
While the project is at the prototype stage, all conversations are stored in plain JSON files in the `data/chats/` folder. No database needs to be set up. When the project scales, we will replace `ChatStorage` with Entity Framework Core without changes to the controller logic.

## Design link
https://www.figma.com/design/g8swP7duhrvI3IWAdLCoYh/CiteDesign?m=auto&t=N4wkvVdnlqKdQ9WJ-1

## Blueprint link
https://docs.google.com/document/d/1o1KD3aPaxhAhXGFQFTv9lYqB_miqYe9v/edit?usp=drive_link&ouid=104580997232242831904&rtpof=true&sd=true
