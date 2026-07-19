// src/pages/Recipes.tsx

import React, { useState, useEffect } from 'react';
import { apiService, Recipe } from '../services/apiService';
import './recipes.css';

const Recipes: React.FC = () => {
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchAllRecipes();
  }, []);

  const fetchAllRecipes = async () => {
    try {
      setLoading(true);
      const data = await apiService.getAllRecipes();
      setRecipes(data);
      setError(null);
    } catch (err) {
      setError('Failed to fetch recipes');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!searchTerm.trim()) {
      fetchAllRecipes();
      return;
    }

    try {
      setLoading(true);
      const response = await apiService.searchRecipesByIngredients([searchTerm]);
      setRecipes(response.recipes);
      setError(null);
    } catch (err) {
      setError('Failed to search recipes');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="recipes">
      <h1>Recipes</h1>
      
      <form onSubmit={handleSearch} className="search-form">
        <input
          type="text"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          placeholder="Search by ingredient..."
          className="search-input"
        />
        <button type="submit" className="search-button">
          Search
        </button>
      </form>

      {loading && <div className="loading">Loading recipes...</div>}
      
      {error && <div className="error">{error}</div>}
      
      {!loading && !error && (
        <div className="recipes-grid">
          {recipes.length > 0 ? (
            recipes.map((recipe) => (
              <div key={recipe.id} className="recipe-card">
                <h3>{recipe.name}</h3>
                <p className="tags">{recipe.tags}</p>
                <p className="prep-time">Prep Time: {recipe.timeToPrepare} min</p>
                <p className="servings">Servings: {recipe.servings}</p>
              </div>
            ))
          ) : (
            <p>No recipes found.</p>
          )}
        </div>
      )}
    </div>
  );
};

export default Recipes;