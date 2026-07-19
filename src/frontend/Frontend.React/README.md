# Freezer Lego Meals Frontend

A React + TypeScript frontend for the Freezer Lego Meals project that connects to the backend API.

## Features

- Recipe browsing and search functionality
- Detailed recipe viewing
- AI assistant chat interface
- Responsive design for all devices
- Clean, modern UI with intuitive navigation

## Requirements

- Node.js 18+ (recommended: LTS version)
- npm 10+ or yarn 1.22+
- Access to one of the backend APIs:
  - .NET implementation at `http://localhost:5001`
  - Python implementation at `http://localhost:5000` 
  - NestJS implementation at `http://localhost:3000`

## Getting Started

### Prerequisites

Make sure you have Node.js and npm installed on your system:

```bash
node --version
npm --version
```

### Installation

1. Navigate to the frontend directory:
   ```bash
   cd src/frontend/Frontend.React
   ```

2. Install project dependencies:
   ```bash
   npm install
   ```

### Running the Application

#### Development Mode
Start the development server with hot-reloading:
```bash
npm run dev
```
The application will be available at [http://localhost:5173](http://localhost:5173)

#### Production Build
Build the optimized production version:
```bash
npm run build
```

#### Preview Production Build
Preview the production build locally:
```bash
npm run preview
```

### Configuration

The API base URL can be configured in `src/config.ts`. By default, it points to the NestJS backend at `http://localhost:3000`, but you can change it to point to any of the three implementations.

## Project Structure

```
src/
├── components/           # Reusable UI components
│   └── Layout/          # Layout components (navbar, footer)
├── pages/               # Page components
├── services/            # API service and data handling
├── App.tsx              # Main application component with routing
├── main.tsx             # Entry point
└── config.ts            # Application configuration
```

## Available Pages

- `/` - Home page
- `/recipes` - Recipe browsing and search
- `/recipes/:id` - Recipe details view
- `/assistant` - AI assistant chat
- `/meal-planner` - Meal planning (placeholder)
- `/shopping-list` - Shopping list feature (placeholder)

## API Integration

The frontend communicates with the backend using a dedicated `ApiService` that handles:
- Recipe retrieval and search
- AI assistant conversations 
- Shopping list generation
- All REST endpoints defined in the backend

## Contributing

This project follows standard React + TypeScript best practices. When contributing:

1. Make sure all changes compile without errors
2. Follow existing code style and patterns
3. Write appropriate tests for new functionality
4. Update documentation when making significant changes
