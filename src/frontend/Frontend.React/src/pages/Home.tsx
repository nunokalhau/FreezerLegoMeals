// src/pages/Home.tsx

import React from 'react';
import { Link } from 'react-router-dom';
import './home.css';

const Home: React.FC = () => {
  return (
    <div className="home">
      <div className="hero">
        <h1>Freezer Lego Meals</h1>
        <p>A modular meal-prep system designed to make batch cooking simpler, more flexible, and easier to scale.</p>
      </div>
      
      <div className="features">
        <h2>Features</h2>
        <p>
          This project helps you build freezer-friendly meal components with reusable ingredients, 
          supports batch cooking and weekly meal prep, and encourages modular recipe design instead of one-off meals.
        </p>
      </div>

      <div className="navigation-cards">
        <h2>Explore</h2>
        <div className="cards-container">
          <div className="card">
            <Link to="/recipes">
              <h3>Recipes</h3>
              <p>Browse and search for recipes</p>
            </Link>
          </div>
          <div className="card">
            <Link to="/assistant">
              <h3>Assistant</h3>
              <p>AI-powered meal assistant</p>
            </Link>
          </div>
          <div className="card">
            <Link to="/meal-planner">
              <h3>Meal Planner</h3>
              <p>Plan your weekly meals</p>
            </Link>
          </div>
          <div className="card">
            <Link to="/shopping-list">
              <h3>Shopping List</h3>
              <p>Generate shopping lists for meals</p>
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Home;