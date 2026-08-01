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

CREATE TABLE recipe_translations (
    recipe_id INTEGER NOT NULL,
    language TEXT NOT NULL,
    name TEXT NOT NULL,
    tags TEXT,
    notes TEXT,
    prepping TEXT,
    translation_version INTEGER NOT NULL DEFAULT 1,
    content_hash TEXT NOT NULL,
    provenance TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    PRIMARY KEY (recipe_id, language),
    FOREIGN KEY (recipe_id) REFERENCES recipes(id) ON DELETE CASCADE
);

CREATE INDEX idx_recipe_translations_language
    ON recipe_translations(language);

CREATE TABLE ingredient_translations (
    ingredient_id INTEGER NOT NULL,
    language TEXT NOT NULL,
    name TEXT NOT NULL,
    unit TEXT,
    translation_version INTEGER NOT NULL DEFAULT 1,
    content_hash TEXT NOT NULL,
    provenance TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    PRIMARY KEY (ingredient_id, language),
    FOREIGN KEY (ingredient_id) REFERENCES ingredients(id) ON DELETE CASCADE
);

CREATE INDEX idx_ingredient_translations_language
    ON ingredient_translations(language);

CREATE TABLE recipe_combination_translations (
    combination_id INTEGER NOT NULL,
    language TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT,
    notes TEXT,
    translation_version INTEGER NOT NULL DEFAULT 1,
    content_hash TEXT NOT NULL,
    provenance TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    PRIMARY KEY (combination_id, language),
    FOREIGN KEY (combination_id) REFERENCES recipe_combinations(id) ON DELETE CASCADE
);

CREATE INDEX idx_recipe_combination_translations_language
    ON recipe_combination_translations(language);

CREATE TABLE tag_translations (
    tag_key TEXT NOT NULL,
    language TEXT NOT NULL,
    display_name TEXT NOT NULL,
    translation_version INTEGER NOT NULL DEFAULT 1,
    content_hash TEXT NOT NULL,
    provenance TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    PRIMARY KEY (tag_key, language)
);

CREATE INDEX idx_tag_translations_language
    ON tag_translations(language);

CREATE TABLE unit_translations (
    unit_key TEXT NOT NULL,
    language TEXT NOT NULL,
    display_name TEXT NOT NULL,
    translation_version INTEGER NOT NULL DEFAULT 1,
    content_hash TEXT NOT NULL,
    provenance TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    PRIMARY KEY (unit_key, language)
);

CREATE INDEX idx_unit_translations_language
    ON unit_translations(language);

CREATE TABLE recipe_ingredient_localizations (
    recipe_id INTEGER NOT NULL,
    ingredient_id INTEGER NOT NULL,
    language TEXT NOT NULL,
    amount_text TEXT,
    unit_text TEXT,
    notes TEXT,
    source_text TEXT,
    translation_version INTEGER NOT NULL DEFAULT 1,
    content_hash TEXT NOT NULL,
    provenance TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    PRIMARY KEY (recipe_id, ingredient_id, language),
    FOREIGN KEY (recipe_id, ingredient_id) REFERENCES recipe_ingredients(recipe_id, ingredient_id) ON DELETE CASCADE
);

CREATE TABLE recipe_index_metadata (
    recipe_id INTEGER PRIMARY KEY,
    language_coverage TEXT NOT NULL,
    projection_fingerprint TEXT NOT NULL,
    projection_schema_version TEXT NOT NULL,
    projection_generated_at_utc TEXT NOT NULL,
    FOREIGN KEY (recipe_id) REFERENCES recipes(id) ON DELETE CASCADE
);
