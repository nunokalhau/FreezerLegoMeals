// src/components/Layout/Navbar.tsx

import React from 'react';
import { Link } from 'react-router-dom';
import './navbar.css';

const Navbar: React.FC = () => {
  return (
    <nav className="navbar">
      <div className="navbar-container">
        <Link to="/" className="navbar-brand">
          Freezer Lego Meals
        </Link>
        <ul className="navbar-nav">
          <li className="nav-item">
            <Link to="/recipes" className="nav-link">
              Recipes
            </Link>
          </li>
          <li className="nav-item">
            <Link to="/assistant" className="nav-link">
              Assistant
            </Link>
          </li>
          <li className="nav-item">
            <Link to="/meal-planner" className="nav-link">
              Meal Planner
            </Link>
          </li>
          <li className="nav-item">
            <Link to="/shopping-list" className="nav-link">
              Shopping List
            </Link>
          </li>
        </ul>
      </div>
    </nav>
  );
};

export default Navbar;