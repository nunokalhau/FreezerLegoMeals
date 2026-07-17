import sqlite3
from pathlib import Path

root = Path(__file__).resolve().parent
db_path = root / 'recipes_local.db'
schema_path = root / 'recipes.sqlite.sql'
seed_path = root / 'recipes_manual_seed.sql'

if db_path.exists():
    db_path.unlink()

conn = sqlite3.connect(db_path)
conn.executescript(schema_path.read_text(encoding='utf-8'))
conn.executescript(seed_path.read_text(encoding='utf-8'))
conn.commit()
conn.close()
print('db_ready')
