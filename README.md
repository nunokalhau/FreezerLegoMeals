# 🧊 Freezer Lego Meals

A modular meal-prep system designed to make batch cooking simpler, more flexible, and easier to scale.

## What this project is

This project solves a practical problem: cooking well during the week is often time-consuming, repetitive, and hard to plan. The solution is to separate meals into reusable components such as proteins, starches, vegetables, sauces, and pickles, then freeze them as modular building blocks.

The repository is both a real-world food system and a portfolio project that demonstrates structured thinking, documentation, and gradual product development. It also serves as a learning space for Python, AI, product thinking, and software engineering.

## What the project does

- helps users build freezer-friendly meal components
- supports batch cooking and weekly meal prep
- encourages modular recipe design instead of one-off meals
- creates a foundation for future automation and AI features

## Who it is for

- people who want a practical weekly meal-prep workflow
- anyone interested in modular cooking systems
- developers and recruiters reviewing a thoughtful, well-documented project

## How the repository is structured

- src/food/ — the main content layer for recipes, meal components, and meal ideas
- src/scripts/ — automation and tooling scripts (source of truth for AI capabilities)
- src/tools/ — AI-native interface including ask.py (human-friendly CLI wrapper)
- tests/ — future validation and quality checks
- .docs/ — architecture, roadmap, and project documentation

## What has been built already

- a modular recipe library with reusable components
- category-based recipe organization
- recipe templates and contribution guidance
- a structured roadmap for future phases
- documentation for batch cooking and freezer-friendly workflows

## What is being built next

- automation with Python
- local AI experimentation
- an AI assistant for repository-aware questions
- semantic search and retrieval-based assistants
- later product-facing features such as a website and richer planning tools

## Local AI Setup

For instructions on setting up a local AI coding environment using OpenCode and Ollama, see the [local AI setup guide](.docs/local-ai-setup.md).

## AI Agent Integration

AI agents should inspect the tools in `src/scripts/` and execute them directly for maximum compatibility. The `src/tools/ask.py` script is provided as a human-friendly CLI wrapper that demonstrates what capabilities are available.

## Showcase

### Sample workflow

1. Choose 3 proteins, 2 bases, and 2 vegetables.
2. Cook everything in one batch session.
3. Portion and freeze each component separately.
4. Combine them into different meals across the week.

### Example meal combination

- turkey chili
- white rice
- roasted vegetables
- cilantro crema

### Example AI direction

A future assistant could answer prompts such as:

- “What can I cook with tofu and rice?”
- “Suggest a vegetarian meal from the repository.”
- “Show me a quick freezer-friendly combo.”

### Example automation direction

A future script could generate:

- recipe indexes
- shopping lists
- metadata validation
- simple meal-planning summaries

## Quick start

1. browse the recipe categories
2. choose a few components to prep
3. follow the batch-cooking workflow
4. combine the frozen blocks into different meals

## Project goals

- reduce weekly cooking friction
- make meal prep more structured and repeatable
- build a strong documentation-first project for learning and portfolio use
- evolve gradually from recipes to automation and AI

## Automation guidelines

When adding automation, prefer generating structured JSON as the primary artifact.

Why:
- JSON is easy for Python scripts, future APIs, and assistants to consume.
- It keeps the project interoperable instead of locking automation to one presentation format.
- Markdown can still be generated from the JSON later for human-readable output in the repository.

Recommended pattern:
1. Parse or scan repository content.
2. Generate JSON with structured metadata.
3. Optionally render a Markdown view from that JSON for readability.

This keeps the repository both human-friendly and machine-friendly.
