# 🧊 Freezer Lego Meals

An AI-powered meal planning platform built as a software engineering portfolio project.

The project combines meal preparation, clean software architecture, multiple backend implementations, local LLMs, semantic search and Retrieval-Augmented Generation (RAG) to simplify freezer meal preparation and weekly meal planning.

---

# What this project is

Freezer Lego Meals started as a modular freezer meal repository.

It has evolved into a complete AI-assisted Food Operating System designed to help users:

- discover recipes
- plan weekly meals
- generate shopping lists
- organize batch cooking
- interact with recipes using natural language

The repository also serves as a software engineering portfolio demonstrating modern backend development, AI integration and clean architecture across multiple technology stacks.

---

# Current Features

## Meal Planning

- Modular freezer-friendly recipes
- Batch cooking workflow
- Weekly meal planning
- Shopping list generation
- Ingredient substitutions
- Serving conversion
- Batch preparation scheduling

## AI

- Local Ollama integration
- AI Assistant
- Tool Calling
- Semantic Search
- Embeddings
- Local Vector Store
- Retrieval-Augmented Generation (RAG)

## APIs

Three equivalent backend implementations:

- .NET
- NestJS
- Python (FastAPI)

Each implementation exposes the same public API and follows the same architectural principles.

## Frontend

- React + TypeScript frontend
- AI Assistant interface
- REST API integration

---

# Repository Structure

```text
src/

    ai/
        Embedding/
        SemanticSearch/
        Retrieval/
        RAG/
        Memory/
        VectorStores/

    orchestration/
        DotNet/
        NestJS/
        Python/

    api/
        WebApi.DotNet/
        WebApi.NestJS/
        WebApi.Python/

    frontend/
        React/

    services/

    repositories/

    domain/

    scripts/

    tools/
```

---

# AI Architecture

The assistant combines multiple AI capabilities.

```text
User

↓

Assistant API

↓

Orchestration

↓

Meal Planning Agent

↓

Semantic Search
Tool Executor
Retrieval

↓

Prompt Builder

↓

Ollama

↓

Answer
```

Current capabilities include:

- Semantic Search
- Tool Calling
- RAG
- Local LLM execution

Planned capabilities include:

- Conversation Memory
- Redis-backed memory
- MCP
- Multiple specialized agents

---

# Technologies

Backend

- .NET
- C#
- ASP.NET Core

- NestJS
- TypeScript

- Python
- FastAPI

AI

- Ollama
- Embeddings
- Semantic Search
- RAG
- Tool Calling

Frontend

- React
- TypeScript

Testing

- Unit Tests
- Integration Tests

---

# Current AI Workflow

Example request:

> "Plan my dinners for next week."

The assistant may perform:

```text
User Request

↓

Semantic Search

↓

Retrieve Recipes

↓

Execute Planning Tools

↓

Generate Shopping List

↓

RAG

↓

Final Response
```

---

# Goals

The project aims to:

- simplify weekly meal planning
- reduce food waste
- reduce decision fatigue
- automate repetitive cooking tasks
- demonstrate modern software engineering
- demonstrate practical AI integration
- serve as a long-term portfolio project

---

# Roadmap

Completed

- Modular recipes
- Automation scripts
- Three backend implementations
- Tool Calling
- AI Assistant
- Embeddings
- Semantic Search
- RAG
- React frontend

Next

- Orchestration layer
- Specialized AI agents
- Conversation memory
- Redis
- MCP
- ChromaDB
- User accounts
- Pantry management
- Nutrition tracking

---

# Running the Project

Each backend includes its own README describing:

- installation
- dependencies
- running locally
- Swagger
- testing

Refer to the documentation inside:

- WebApi.DotNet
- WebApi.NestJS
- WebApi.Python

---

# Philosophy

This project is intentionally iterative.

Every new feature should:

- solve a real problem
- remain well documented
- be implemented consistently across .NET, NestJS and Python
- include tests
- keep the architecture clean

The repository is both the product and the learning journey.