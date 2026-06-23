# 🤖 AgentFlow — Multi-Agent Customer Support Automation Platform

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Semantic Kernel](https://img.shields.io/badge/Semantic%20Kernel-1.15-blue)](https://github.com/microsoft/semantic-kernel)
[![React](https://img.shields.io/badge/React-18-61DAFB?logo=react)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.2-3178C6?logo=typescript)](https://www.typescriptlang.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

An enterprise-grade, multi-agent AI platform that automates customer support email handling using **Microsoft Semantic Kernel**, **C# .NET 8**, and a **React** operator dashboard. Five autonomous AI agents collaborate through a structured pipeline — classifying intent, retrieving knowledge via RAG, drafting responses, reviewing quality, and awaiting human approval — before dispatching replies.

---

## 📸 Dashboard Preview

The operator dashboard provides real-time visibility into the multi-agent pipeline:

- **Left Panel** — Live ticket queue with status badges and intent classification tags
- **Center Panel** — Step-by-step agent execution logs with input/output traces
- **Right Panel** — RAG source documents, AI-generated draft editor, review scores, and human approval controls

---

## 🏗️ Architecture

```
Customer Email
      ↓
┌─────────────────────────┐
│  Intent Classification  │  → Extracts intent, category, metadata (CRM/Order IDs)
│        Agent            │
└──────────┬──────────────┘
           ↓
┌─────────────────────────┐
│  Knowledge Retrieval    │  → Vector similarity search (pgvector / local cosine)
│     Agent (RAG)         │
└──────────┬──────────────┘
           ↓
┌─────────────────────────┐
│  Response Generation    │  → Drafts email using tools (CRM Plugin, Order Plugin)
│        Agent            │
└──────────┬──────────────┘
           ↓
┌─────────────────────────┐
│   Quality Review        │  → Scores draft (0-10), auto-retries if score < 7.0
│        Agent            │
└──────────┬──────────────┘
           ↓
┌─────────────────────────┐
│   Human Approval        │  → Operator reviews, edits, approves or rejects
│      Gateway            │
└──────────┬──────────────┘
           ↓
       Email Sent ✉️
```

---

## ✨ Key Features

### 🤖 Multi-Agent System
| Agent | Role |
|-------|------|
| **Intent Classification** | Classifies email intent (Shipping, Refund, TechSupport, Escalation) and extracts structured metadata |
| **Knowledge Retrieval (RAG)** | Searches vector-embedded knowledge base using cosine similarity for relevant policy documents |
| **Response Generation** | Drafts professional email responses using retrieved context and tool-augmented data |
| **Quality Review** | Audits drafts for accuracy, completeness, tone, and policy compliance with a 0-10 score |
| **Workflow Orchestrator** | Coordinates the full pipeline with retry logic (up to 3 attempts) and error handling |

### 🔧 Tool Calling
- **CRM Plugin** — Retrieves customer profiles (name, tier, lifetime spend, account history)
- **Order Plugin** — Looks up order status, shipping carrier, tracking URLs, delivery estimates

### 🧠 RAG & Vector Database
- **PostgreSQL + pgvector** — Production-grade vector storage with `vector(1536)` columns and `<=>` cosine distance queries
- **SQLite Fallback** — Automatic in-memory cosine similarity calculation when PostgreSQL is offline
- **OpenAI Embeddings** — `text-embedding-3-small` for document vectorization

### 🔄 Retry Logic & Agent Communication
- Quality Review Agent auto-rejects low-scoring drafts (< 7.0/10)
- Orchestrator retries the draft → review loop up to 3 times
- Operator rejection triggers a full re-generation cycle with feedback context

### 👤 Human-in-the-Loop
- Operator can **edit** the draft directly in the textarea editor
- **Approve & Send** — Dispatches the email to the customer
- **Reject & Rewrite** — Sends feedback to the Response Generation Agent, triggering a new draft version

### 💾 Dual Database Support
- **PostgreSQL** — Attempted first on startup (port 5432)
- **SQLite** — Automatic fallback with zero configuration (`customersupport.db`)
- Auto-migration with `EnsureCreated()` and knowledge base seeding

### 🎭 Offline Simulation Mode
- Runs fully without an OpenAI API key
- All agents use rule-based engines and keyword matching to simulate LLM behavior
- CRM/Order plugins return mock data for demo purposes
- Seamlessly switches to real LLM when an API key is provided

---

## 🚀 Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/) (for the React dashboard)
- *(Optional)* [Docker](https://www.docker.com/) for PostgreSQL
- *(Optional)* [OpenAI API Key](https://platform.openai.com/api-keys) for real LLM responses

### 1. Clone the Repository

```bash
git clone https://github.com/zaidi-cj/customer-support-automation.git
cd customer-support-automation
```

### 2. Start the Backend API

```bash
cd backend/CustomerSupportAgent.Api
dotnet run
```

The API starts at **http://localhost:5144** with Swagger docs at `/swagger`.

> **Note:** Without PostgreSQL, it automatically falls back to SQLite. Without an OpenAI key, it runs in offline simulation mode.

### 3. Start the Frontend Dashboard

```bash
cd frontend
npm install
npm run dev
```

The dashboard opens at **http://localhost:3000**.

### 4. *(Optional)* Start PostgreSQL with Docker

```bash
docker-compose up -d
```

This launches PostgreSQL on port 5432 with pgvector extension enabled.

### 5. *(Optional)* Configure OpenAI

Edit `backend/CustomerSupportAgent.Api/appsettings.json`:

```json
{
  "OpenAI": {
    "ApiKey": "sk-your-api-key-here",
    "ModelId": "gpt-4o",
    "EmbeddingModelId": "text-embedding-3-small"
  }
}
```

---

## 📁 Project Structure

```
customer-support-automation/
├── backend/
│   ├── CustomerSupportAgent.Api/          # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   │   ├── TicketsController.cs       # CRUD + workflow trigger endpoints
│   │   │   ├── ApprovalController.cs      # Human approval/rejection endpoints
│   │   │   └── KnowledgeController.cs     # Knowledge base management
│   │   ├── Program.cs                     # DI configuration, DB setup, seeding
│   │   └── appsettings.json               # OpenAI & database configuration
│   │
│   ├── CustomerSupportAgent.Core/         # Domain models & interfaces
│   │   ├── Models/                        # Ticket, TicketDraft, AgentLog, etc.
│   │   ├── Interfaces/                    # ISupportAgent, ITicketRepository
│   │   └── Orchestrator/
│   │       └── WorkflowOrchestrator.cs    # Multi-agent pipeline coordinator
│   │
│   └── CustomerSupportAgent.Infrastructure/
│       ├── Agents/                        # All 4 agent implementations
│       │   ├── IntentClassificationAgent.cs
│       │   ├── KnowledgeRetrievalAgent.cs
│       │   ├── ResponseGenerationAgent.cs
│       │   └── QualityReviewAgent.cs
│       ├── Plugins/                       # Semantic Kernel native plugins
│       │   ├── CrmPlugin.cs              # Customer profile lookup
│       │   └── OrderPlugin.cs            # Order status & tracking lookup
│       ├── Data/
│       │   └── AppDbContext.cs           # EF Core context (PostgreSQL/SQLite)
│       ├── Repositories/
│       │   └── TicketRepository.cs
│       └── Services/
│           └── KnowledgeBaseService.cs   # RAG indexing & vector search
│
├── frontend/
│   ├── src/
│   │   ├── App.tsx                       # Full operator dashboard (978 lines)
│   │   ├── index.css                     # Premium dark-mode design system
│   │   └── main.tsx                      # React entry point
│   ├── index.html
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
│
├── docker-compose.yml                    # PostgreSQL + pgvector container
└── .gitignore
```

---

## 🔌 API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/tickets` | List all tickets with drafts |
| `GET` | `/api/tickets/{id}` | Get ticket details with logs and drafts |
| `POST` | `/api/tickets` | Create a new ticket and trigger the agent pipeline |
| `POST` | `/api/tickets/{id}/run` | Re-run the agent pipeline on an existing ticket |
| `POST` | `/api/approval/{id}/approve` | Approve draft (with optional edits) and send email |
| `POST` | `/api/approval/{id}/reject` | Reject draft with feedback and trigger rewrite |
| `GET` | `/api/knowledge` | List all knowledge base documents |
| `POST` | `/api/knowledge` | Index a new knowledge document with vector embedding |

Full Swagger documentation available at **http://localhost:5144/swagger**

---

## 🧪 Testing the Platform

### Quick Demo (No API Key Required)

1. Start backend and frontend (see Quick Start above)
2. Click **"New Customer Ticket"** in the dashboard
3. Select a **Quick Testing Preset** (e.g., "Order Status Tracking")
4. Click **"Submit & Run Workflow"**
5. Watch the agents execute in real-time on the timeline
6. Review the generated draft, edit if needed
7. Click **"Approve & Send"** or **"Reject & Rewrite"** with feedback

### Pre-seeded Test Data

The platform auto-seeds on first run:
- **4 Knowledge Base Articles**: Refund Policy, Shipping & Tracking, Login Troubleshooting, Escalation Protocol
- **1 Sample Ticket**: Jane Doe asking about order 10293 tracking

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| **Backend Framework** | ASP.NET Core (.NET 8) |
| **AI Orchestration** | Microsoft Semantic Kernel 1.15 |
| **LLM Provider** | OpenAI (GPT-4o) with offline simulation fallback |
| **Embeddings** | OpenAI text-embedding-3-small (1536 dimensions) |
| **Primary Database** | PostgreSQL 16 + pgvector extension |
| **Fallback Database** | SQLite with in-memory cosine similarity |
| **ORM** | Entity Framework Core 8 |
| **Frontend** | React 18 + TypeScript 5.2 |
| **Build Tool** | Vite 5 |
| **Icons** | Lucide React |
| **Containerization** | Docker Compose |

---

## 📄 License

This project is open source under the [MIT License](LICENSE).

---

## 🤝 Contributing

Contributions are welcome! Please open an issue or submit a pull request.

---

Built with ❤️ using Microsoft Semantic Kernel and .NET 8
