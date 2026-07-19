// src/pages/RecipeDetails.tsx

import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { apiService } from '../services/apiService';
import type { Recipe } from '../services/apiService';
import './recipe-details.css';

const RecipeDetails: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [recipe, setRecipe] = useState<Recipe | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (id) {
      fetchRecipeDetails(parseInt(id));
    }
  }, [id]);

  const fetchRecipeDetails = async (recipeId: number) => {
    try {
      setLoading(true);
      const data = await apiService.getRecipeDetails(recipeId);
      setRecipe(data.recipe);
      setError(null);
    } catch (err) {
      setError('Failed to fetch recipe details');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleBackClick = () => {
    navigate('/recipes');
  };

  if (loading) {
    return <div className="loading">Loading recipe details...</div>;
  }

  if (error || !recipe) {
    return (
      <div className="recipe-details">
        <div className="error">{error || 'Recipe not found'}</div>
        <button onClick={handleBackClick} className="back-button">
          Back to Recipes
        </button>
      </div>
    );
  }

  return (
    <div className="recipe-details">
      <button onClick={handleBackClick} className="back-button">
        ← Back to Recipes
      </button>
      
      <div className="recipe-header">
        <h1>{recipe.name}</h1>
        <p className="recipe-tags">{recipe.tags}</p>
      </div>

      <div className="recipe-content">
        <div className="recipe-meta">
          <p><strong>Prep Time:</strong> {recipe.timeToPrepare} minutes</p>
          <p><strong>Servings:</strong> {recipe.servings}</p>
        </div>

        <div className="recipe-description">
          <h2>Description</h2>
          <p>{recipe.notes}</p>
        </div>

        <div className="recipe-ingredients">
          <h2>Ingredients</h2>
          <ul>
            {recipe.recipeIngredients.map((ingredient) => (
              <li key={ingredient.ingredientId}>
                {ingredient.amount} {ingredient.unit} {ingredient.ingredient.name}
              </li>
            ))}
          </ul>
        </div>

        <div className="recipe-steps">
          <h2>Steps</h2>
          <p>{recipe.prepping}</p>
        </div>

        <div className="recipe-notes">
          <h2>Freezing & Reheating Notes</h2>
          <p>{recipe.freezingNotes}</p>
          <p>{recipe.reheatNotes}</p>
        </div>
      </div>

      <div className="recipe-buttons">
        <button className="primary-button">Ask Assistant About This Recipe</button>
        <button className="secondary-button">Generate Shopping List</button>
        <button className="secondary-button">Scale Recipe</button>
      </div>
    </div>
  );
};

export default RecipeDetails;