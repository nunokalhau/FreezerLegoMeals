# Freezer Lego Meals Python API

This is the Python web API for Freezer Lego Meals project.

## Getting Started

### Prerequisites
- Python 3.8+

### Installation
1. Create a virtual environment:
```
python -m venv venv
```

2. Activate the virtual environment:
```
venv\Scripts\Activate.ps1
```

3. Install dependencies:
```
pip install -r requirements.txt
```

### Running the API
```
python app.py
```

The API will be available at `http://localhost:5000`

## Endpoints
- `GET /` - Root endpoint
- `GET /health` - Health check endpoint