// src/components/Layout/Footer.tsx

import React from 'react';
import './footer.css';

const Footer: React.FC = () => {
  return (
    <footer className="footer">
      <div className="footer-container">
        <p>© {new Date().getFullYear()} Freezer Lego Meals. All rights reserved.</p>
      </div>
    </footer>
  );
};

export default Footer;