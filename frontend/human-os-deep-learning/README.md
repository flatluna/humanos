# Human OS — Deep Learning UI

Modern learning platform UI following Human OS pedagogical principles.

## Features

- 🧠 **Recall-first workflow** — activate memory before instruction
- 💭 **Hypothesis stage** — predict before seeing answers  
- ✍️ **Evidence production** — learner creates, not consumes
- 🎯 **Bilingual** — English/Spanish support
- 🏗️ **Scalable architecture** — domain logic separated from React

## Quick Start

```bash
npm install
npm run dev
```

## Project Structure

```
src/
├── features/        Main app sections (Home, Capabilities, etc)
├── runtime/         Session runtime (the core learning experience)
├── domain/          Business logic (no React)
├── i18n/            Translations (EN/ES)
├── components/      Shared UI components
├── lib/             Utilities & API
└── styles/          Global styles
```

## Phase 1 MVP

- ✅ Home dashboard
- ✅ Capabilities browser
- ✅ Sessions (runtime)
- ✅ Evidence gallery
- ✅ i18n (EN/ES)

## Phase 2+ (Coming)

- 🔮 Memory engine (SM-2)
- 🔮 AI Coach
- 🔮 Organizations

---

Built with React + TypeScript + Tailwind CSS + Vite
