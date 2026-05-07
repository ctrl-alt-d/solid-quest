# 🎮 Solid Quest

**Kahoot-style multiplayer quiz game for the classroom, the conference, or your next game night.**

> Another fine example of **Clown Computing** ☁️🤡 — because why not run a real-time quiz game with Blazor Server circuits and zero JavaScript?

## What is this?

Solid Quest is a real-time quiz game where:
- Players join with a **username only** (no passwords, no sign-up friction)
- Admin loads a quest (YAML file with questions), previews it, and starts the game
- Players answer via **mobile-optimized buttons** (no text, just colors)
- Admin projects the **full question + image + countdown** on a big screen
- Progressive scoring rewards speed: faster answers = more points
- Results reveal automatically when time runs out or everyone finishes
- Final leaderboard shows Olympic-style podium for top 3

Built with **Blazor Server**, because sometimes the best solution is the one that makes people ask "wait, you can do that?!"

## Features

- 🎯 **No rooms, no lobbies** — single shared session, everyone plays together
- ⚡ **Real-time everything** — voting, countdowns, results (Blazor circuits handle it)
- 📱 **Mobile player UI** — big color-coded buttons, hidden text (accessibility labels preserved)
- 🖥️ **Projection-optimized admin view** — scales to any screen, no scrolling required
- 🏆 **Progressive scoring** — first question 12 pts, each +50% (configurable)
- ⏱️ **Configurable timeout** — 5-300 seconds per question, default 30s
- 🔄 **F5-friendly** — 1-hour restore cookie, players survive refresh
- 📝 **Markdown support** — questions, answers, explanations (including code blocks)
- 🎨 **Gaming aesthetic** — dark mode, neon accents, answer label badges on card borders
- ♿ **AAA contrast** — where practical, because accessibility matters
- 🧪 **59 tests passing** — backend + UI (bUnit)

## Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Run it

```bash
git clone https://github.com/ctrl-alt-d/solid-quest.git
cd solid-quest
dotnet run --project QuestUI
```

Open browser: `https://localhost:5001` (or whatever port it tells you)

### Play a game

1. **Login as admin** (username: `admin` by default)
2. **Load a quest** (leave URL empty to use embedded sample, or paste your own YAML URL)
3. **Preview questions** (optional, arrows to navigate)
4. **Wait for players** (they join with any username)
5. **Start the game** (button turns green when quest loaded)
6. Players answer on mobile, you project admin view on big screen
7. Watch the leaderboard climb, crown the winner 👑

## Configuration

Edit `QuestUI/appsettings.json`:

```json
{
  "Quest": {
    "AdminUserName": "admin",
    "ProgressiveScoring": true
  }
}
```

- **AdminUserName**: Who gets admin powers (default: `admin`)
- **ProgressiveScoring**: First question = 12 pts, each +50% truncated (default: `true`)

## Creating Quests

Quests are **YAML files** with metadata, questions, and answers.

📖 **[Full YAML Schema](docs/quest-schema.md)**

📦 **[Sample Quests](samples/quests/)**

### Minimal example

```yaml
title: My First Quest
image: https://example.com/quest-cover.jpg  # optional
imageAlt: Quest cover illustration          # optional

questions:
  - text: What is 2 + 2?
    image: https://example.com/math.png     # optional
    imageAlt: Math problem illustration     # optional
    options:
      - "3"
      - "4"
      - "5"
      - "22"
    correct: 2  # 1-indexed (option "4" is correct)
    explanation: |
      Basic arithmetic. **4** is correct.
      
      ```python
      >>> 2 + 2
      4
      ```
```

**Host it anywhere** (GitHub raw, gist, your own server) and paste the URL in the settings card.

## How Scoring Works

### Progressive Scoring (default)

- **First question**: 12 points
- **Each subsequent**: previous × 1.5, truncated
- **Example**: 12 → 18 → 27 → 40 → 60 → 90...
- **Tie-breaker**: Lowest total answer time wins

### Static Scoring (fallback)

- **Every question**: 1 point
- **Tie-breaker**: Still total time

Toggle in settings (only visible before loading quest).

## Architecture

```
QuestBackend/          ← Core logic (session, scoring, timing)
QuestBackendTest/      ← Backend tests (30 passing)
QuestUI/               ← Blazor Server app
  Components/Pages/    ← Home.razor (orchestration)
  Components/Quiz/     ← QuestionCard, ResultCard, etc.
  Auth/                ← Cookie-based username auth
QuestUITest/           ← bUnit component tests (29 passing)
```

**No SignalR setup required** — Blazor Server circuits handle all real-time updates.

## Deployment

### Development

```bash
dotnet watch --project QuestUI
```

### Production

```bash
dotnet publish QuestUI -c Release -o ./publish
cd publish
dotnet QuestUI.dll
```

Set environment variables:
```bash
export Quest__AdminUserName=yourname
export Quest__ProgressiveScoring=true
```

Or use Docker:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY publish/ .
EXPOSE 8080
ENTRYPOINT ["dotnet", "QuestUI.dll"]
```

## Why Blazor Server?

Because **Clown Computing** 🤡 — it's fun, it works, and it makes people uncomfortable at meetups when you explain there's no REST API, no GraphQL, no tRPC... just circuits and render trees.

Also:
- Zero client-side bundling (it's just HTML over WebSocket)
- Real-time by default (no polling, no manual sync)
- C# everywhere (no context switching)
- Server-side validation is the only validation

Trade-offs:
- Requires sticky sessions (load balancer must route by circuit)
- Not for 10k concurrent users (but fine for classroom/conference scale)
- Bad network = bad time

## License

MIT — see [LICENSE](LICENSE)

## Contributing

PRs welcome. Keep tests passing (`dotnet test`), follow existing style, don't break the clown aesthetic.

## Credits

Built by [@ctrl-alt-d](https://github.com/ctrl-alt-d) because traditional cloud architecture is overrated and sometimes a WebSocket-powered monolith is the right answer.

---

*Now go forth and quest.* 🎮
