-- SQLite schema for freezer lego meals
-- Run this in DB Browser or sqlite3 to create the database

CREATE TABLE ingredients (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    unit TEXT
);

CREATE TABLE recipes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    source_path TEXT,
    tags TEXT,
    servings INTEGER,
    time_to_prepare INTEGER,
    prepping TEXT,
    freezing_notes TEXT,
    reheat_notes TEXT,
    combinations TEXT,
    notes TEXT
);

CREATE TABLE recipe_combinations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    description TEXT,
    notes TEXT
);

CREATE TABLE recipe_combination_items (
    combination_id INTEGER NOT NULL,
    recipe_id INTEGER NOT NULL,
    position INTEGER NOT NULL,
    notes TEXT,
    PRIMARY KEY (combination_id, recipe_id),
    FOREIGN KEY (combination_id) REFERENCES recipe_combinations(id) ON DELETE CASCADE,
    FOREIGN KEY (recipe_id) REFERENCES recipes(id) ON DELETE CASCADE
);

CREATE TABLE recipe_ingredients (
    recipe_id INTEGER NOT NULL,
    ingredient_id INTEGER NOT NULL,
    amount REAL,
    amount_text TEXT,
    unit TEXT,
    notes TEXT,
    source_text TEXT,
    PRIMARY KEY (recipe_id, ingredient_id),
    FOREIGN KEY (recipe_id) REFERENCES recipes(id) ON DELETE CASCADE,
    FOREIGN KEY (ingredient_id) REFERENCES ingredients(id) ON DELETE RESTRICT
);

CREATE INDEX idx_recipe_ingredients_ingredient_id
    ON recipe_ingredients(ingredient_id);
